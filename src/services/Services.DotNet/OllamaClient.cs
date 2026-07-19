using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Services.DotNet;

public class OllamaClient : IOllamaClient
{
    private readonly HttpClient _httpClient;
    private readonly OllamaOptions _options;

    public OllamaClient(HttpClient httpClient, IOptions<OllamaOptions> options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
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

        using var response = await SendChatAsync(selectedModel, messages, tools, cancellationToken);
        response.EnsureSuccessStatusCode();

        var chatResponse = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(cancellationToken);
        var toolCalls = chatResponse?.Message?.ToolCalls?
            .Where(toolCall => !string.IsNullOrWhiteSpace(toolCall.Function?.Name))
            .Select(toolCall => AssistantToolCall.FromJsonArguments(toolCall.Function!.Name, toolCall.Function.Arguments))
            .ToList() ?? [];

        return new OllamaChatResult(chatResponse?.Message?.Content ?? string.Empty, toolCalls);
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