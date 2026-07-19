using System.Net.Http.Json;
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

    public async Task<string> ChatAsync(string? model, IReadOnlyList<ConversationMessage> messages, CancellationToken cancellationToken = default)
    {
        if (messages == null || messages.Count == 0)
            throw new ArgumentException("At least one chat message is required", nameof(messages));

        var selectedModel = string.IsNullOrWhiteSpace(model) ? _options.DefaultModel : model;
        if (string.IsNullOrWhiteSpace(selectedModel))
            throw new InvalidOperationException("An Ollama model must be provided or configured as the default model.");

        var request = new OllamaChatRequest(
            selectedModel,
            messages.Select(message => new OllamaChatMessage(ToOllamaRole(message.Role), message.Content)),
            Stream: false);

        using var response = await _httpClient.PostAsJsonAsync("api/chat", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var chatResponse = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(cancellationToken);
        return chatResponse?.Message?.Content ?? string.Empty;
    }

    private sealed record OllamaChatRequest(string Model, IEnumerable<OllamaChatMessage> Messages, bool Stream);

    private sealed record OllamaChatMessage(string Role, string Content);

    private sealed record OllamaChatResponse(OllamaChatMessage? Message);

    private static string ToOllamaRole(ConversationRole role)
    {
        return role switch
        {
            ConversationRole.System => "system",
            ConversationRole.User => "user",
            ConversationRole.Assistant => "assistant",
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
        };
    }
}