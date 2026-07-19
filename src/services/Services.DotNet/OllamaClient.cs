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

    public async Task<string> ChatAsync(string? model, string userMessage, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
            throw new ArgumentException("User message is required", nameof(userMessage));

        var selectedModel = string.IsNullOrWhiteSpace(model) ? _options.DefaultModel : model;
        if (string.IsNullOrWhiteSpace(selectedModel))
            throw new InvalidOperationException("An Ollama model must be provided or configured as the default model.");

        var request = new OllamaChatRequest(
            selectedModel,
            [new OllamaChatMessage("user", userMessage)],
            Stream: false);

        using var response = await _httpClient.PostAsJsonAsync("api/chat", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var chatResponse = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(cancellationToken);
        return chatResponse?.Message?.Content ?? string.Empty;
    }

    private sealed record OllamaChatRequest(string Model, IEnumerable<OllamaChatMessage> Messages, bool Stream);

    private sealed record OllamaChatMessage(string Role, string Content);

    private sealed record OllamaChatResponse(OllamaChatMessage? Message);
}