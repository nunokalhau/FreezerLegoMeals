using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Services.DotNet;

public sealed class OllamaModelCapabilitiesProvider : IModelCapabilitiesProvider
{
    private readonly IModelCapabilitiesCache _cache;
    private readonly ILogger<OllamaModelCapabilitiesProvider> _logger;

    public OllamaModelCapabilitiesProvider(
        IModelCapabilitiesCache cache,
        ILogger<OllamaModelCapabilitiesProvider>? logger = null)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? NullLogger<OllamaModelCapabilitiesProvider>.Instance;
    }

    public async Task<ModelCapabilities> GetCapabilitiesAsync(string model, CancellationToken cancellationToken = default)
    {
        var normalizedModel = NormalizeModel(model);
        var capabilities = await _cache.GetAsync(normalizedModel, cancellationToken);
        return capabilities ?? ModelCapabilities.Unknown(normalizedModel);
    }

    public async Task RecordChatResultAsync(
        string model,
        bool toolsWereRequested,
        HttpStatusCode statusCode,
        bool requestSucceeded,
        string? responseBody,
        CancellationToken cancellationToken = default)
    {
        if (!toolsWereRequested)
        {
            return;
        }

        var normalizedModel = NormalizeModel(model);
        var existing = await _cache.GetAsync(normalizedModel, cancellationToken) ?? ModelCapabilities.Unknown(normalizedModel);
        var supportsToolCalling = existing.SupportsToolCalling;

        if (requestSucceeded)
        {
            supportsToolCalling = true;
        }
        else if (statusCode == HttpStatusCode.BadRequest && IndicatesToolUnsupported(responseBody))
        {
            supportsToolCalling = false;
        }

        if (supportsToolCalling == existing.SupportsToolCalling)
        {
            return;
        }

        await _cache.SetAsync(existing with { SupportsToolCalling = supportsToolCalling }, cancellationToken);
        _logger.LogInformation(
            "Model capability learned provider={Provider} model={Model} supportsToolCalling={SupportsToolCalling}",
            "ollama",
            normalizedModel,
            supportsToolCalling);
    }

    private static string NormalizeModel(string model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        return model.Trim();
    }

    private static bool IndicatesToolUnsupported(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return false;
        }

        var message = ExtractErrorText(responseBody).ToLowerInvariant();
        if (!message.Contains("tool", StringComparison.Ordinal))
        {
            return false;
        }

        return message.Contains("does not support", StringComparison.Ordinal)
            || message.Contains("doesn't support", StringComparison.Ordinal)
            || message.Contains("not supported", StringComparison.Ordinal)
            || message.Contains("unsupported", StringComparison.Ordinal);
    }

    private static string ExtractErrorText(string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("error", out var errorProperty)
                && errorProperty.ValueKind == JsonValueKind.String)
            {
                return errorProperty.GetString() ?? responseBody;
            }
        }
        catch (JsonException)
        {
            // If response is not JSON, continue using raw body text.
        }

        return responseBody;
    }
}
