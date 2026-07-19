using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Embedding.DotNet;

public sealed class OllamaEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly EmbeddingOptions _options;

    public OllamaEmbeddingService(HttpClient httpClient, IOptions<EmbeddingOptions> options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<EmbeddingResponse> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text is required to generate an embedding", nameof(text));

        var model = string.IsNullOrWhiteSpace(_options.Model) ? "nomic-embed-text" : _options.Model;
        using var response = await _httpClient.PostAsJsonAsync("api/embeddings", new OllamaEmbeddingRequest(model, text), cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>(cancellationToken);
        if (payload?.Embedding is null || payload.Embedding.Count == 0)
            throw new InvalidOperationException("Ollama did not return an embedding vector.");

        return new EmbeddingResponse(model, payload.Embedding.Count, payload.Embedding);
    }

    private sealed record OllamaEmbeddingRequest(string Model, string Prompt);

    private sealed record OllamaEmbeddingResponse(IReadOnlyList<float> Embedding);
}