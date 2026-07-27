using System.Net;
using System.Net.Http.Json;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Services.DotNet;

public class OllamaClient : IOllamaClient
{
    private static readonly ActivitySource ActivitySource = new("FreezerLegoMeals.AI");
    private readonly HttpClient _httpClient;
    private readonly OllamaOptions _options;
    private readonly ILogger<OllamaClient> _logger;

    public OllamaClient(HttpClient httpClient, IOptions<OllamaOptions> options, ILogger<OllamaClient>? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? NullLogger<OllamaClient>.Instance;
    }

    public async Task<OllamaChatResult> ChatAsync(
        string? model,
        IReadOnlyList<ConversationMessage> messages,
        IReadOnlyList<ToolDefinition> tools,
        CancellationToken cancellationToken = default)
    {
        if (messages == null || messages.Count == 0)
            throw new ArgumentException("At least one chat message is required", nameof(messages));

        var selectedModel = string.IsNullOrWhiteSpace(model) ? _options.DefaultModel : model;
        if (string.IsNullOrWhiteSpace(selectedModel))
            throw new InvalidOperationException("An Ollama model must be provided or configured as the default model.");

        using var activity = ActivitySource.StartActivity("llm.ollama.chat", ActivityKind.Client);
        activity?.SetTag("llm.provider", "ollama");
        activity?.SetTag("llm.model", selectedModel);
        activity?.SetTag("llm.message_count", messages.Count);
        activity?.SetTag("llm.tool_count", tools.Count);

        var startedAt = Stopwatch.StartNew();

        try
        {
            using var response = await SendChatAsync(selectedModel, messages, tools, cancellationToken);
            activity?.SetTag("llm.http_status_code", (int)response.StatusCode);
            response.EnsureSuccessStatusCode();

            var chatResponse = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(cancellationToken);
            var toolCalls = chatResponse?.Message?.ToolCalls?
                .Where(toolCall => !string.IsNullOrWhiteSpace(toolCall.Function?.Name))
                .Select(toolCall => AssistantToolCall.FromJsonArguments(toolCall.Function!.Name, toolCall.Function.Arguments))
                .ToList() ?? [];

            startedAt.Stop();
            var contentLength = chatResponse?.Message?.Content?.Length ?? 0;
            _logger.LogInformation(
                "LLM chat completed provider={Provider} model={Model} messageCount={MessageCount} toolCount={ToolCount} toolCallCount={ToolCallCount} contentLength={ContentLength} statusCode={StatusCode} latencyMs={LatencyMs}",
                "ollama",
                selectedModel,
                messages.Count,
                tools.Count,
                toolCalls.Count,
                contentLength,
                (int)response.StatusCode,
                startedAt.Elapsed.TotalMilliseconds);
            activity?.SetTag("llm.tool_call_count", toolCalls.Count);
            activity?.SetTag("llm.content_length", contentLength);
            activity?.SetTag("llm.latency_ms", startedAt.Elapsed.TotalMilliseconds);

            return new OllamaChatResult(chatResponse?.Message?.Content ?? string.Empty, toolCalls);
        }
        catch (Exception exception) when (exception is BrokenCircuitException || exception is TimeoutRejectedException || exception is HttpRequestException || exception is TaskCanceledException)
        {
            startedAt.Stop();
            _logger.LogWarning(
                exception,
                "LLM dependency failure provider={Provider} model={Model} messageCount={MessageCount} toolCount={ToolCount} latencyMs={LatencyMs}",
                "ollama",
                selectedModel,
                messages.Count,
                tools.Count,
                startedAt.Elapsed.TotalMilliseconds);
            activity?.SetTag("llm.failure", exception.GetType().Name);
            activity?.SetTag("llm.latency_ms", startedAt.Elapsed.TotalMilliseconds);
            throw;
        }
    }

    private async Task<HttpResponseMessage> SendChatAsync(
        string selectedModel,
        IReadOnlyList<ConversationMessage> messages,
        IReadOnlyList<ToolDefinition> tools,
        CancellationToken cancellationToken)
    {
        var request = new OllamaChatRequest(
            selectedModel,
            messages.Select(message => new OllamaChatMessage(ToOllamaRole(message.Role), message.Content)),
            tools.Select(ToOllamaTool),
            Stream: false);

        var response = await _httpClient.PostAsJsonAsync("api/chat", request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.BadRequest || tools.Count == 0)
            return response;

        _logger.LogWarning(
            "LLM chat returned bad request with tools enabled; retrying without tools model={Model} toolCount={ToolCount}",
            selectedModel,
            tools.Count);
        response.Dispose();
        var fallbackRequest = request with { Tools = [] };
        return await _httpClient.PostAsJsonAsync("api/chat", fallbackRequest, cancellationToken);
    }

    private sealed record OllamaChatRequest(string Model, IEnumerable<OllamaChatMessage> Messages, IEnumerable<OllamaTool> Tools, bool Stream);

    private sealed record OllamaChatMessage(string Role, string Content);

    private sealed record OllamaTool(string Type, OllamaToolFunction Function);

    private sealed record OllamaToolFunction(string Name, string Description, OllamaToolParameters Parameters);

    private sealed record OllamaToolParameters(string Type, Dictionary<string, OllamaToolProperty> Properties, IReadOnlyList<string> Required);

    private sealed record OllamaToolProperty(string Type, string Description);

    private sealed record OllamaToolCall(OllamaToolCallFunction? Function);

    private sealed record OllamaToolCallFunction(string Name, JsonElement Arguments);

    private sealed record OllamaChatResponseMessage(
        string Role,
        string? Content,
        [property: JsonPropertyName("tool_calls")] IReadOnlyList<OllamaToolCall>? ToolCalls);

    private sealed record OllamaChatResponse(OllamaChatResponseMessage? Message);

    private static string ToOllamaRole(ConversationRole role)
    {
        return role switch
        {
            ConversationRole.System => "system",
            ConversationRole.User => "user",
            ConversationRole.Assistant => "assistant",
            ConversationRole.Tool => "tool",
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
        };
    }

    private static OllamaTool ToOllamaTool(ToolDefinition tool)
    {
        var properties = tool.Parameters
            .Select(parameter => parameter.TrimStart('-').Replace("-", "_"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                parameter => parameter,
                parameter => new OllamaToolProperty("string", $"Parameter for {tool.Name}"),
                StringComparer.OrdinalIgnoreCase);

        return new OllamaTool(
            "function",
            new OllamaToolFunction(
                tool.Name,
                BuildToolDescription(tool),
                new OllamaToolParameters("object", properties, [])));
    }

    private static string BuildToolDescription(ToolDefinition tool)
    {
        var parts = new List<string> { tool.Description };

        if (!string.IsNullOrWhiteSpace(tool.OutputDescription))
        {
            parts.Add($"Output: {tool.OutputDescription}");
        }

        if (tool.ResultExample is not null)
        {
            parts.Add($"Result example: {JsonSerializer.Serialize(tool.ResultExample)}");
        }

        return string.Join("\n", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }
}