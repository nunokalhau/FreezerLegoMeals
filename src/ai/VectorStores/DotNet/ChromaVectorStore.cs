using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace VectorStores.DotNet;

public sealed class ChromaVectorStore : IVectorStore
{
    public const string HttpClientName = "ChromaVectorStore";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ChromaVectorStoreOptions _options;
    private readonly SemaphoreSlim _collectionLock = new(1, 1);
    private string? _collectionId;

    public ChromaVectorStore(HttpClient httpClient, IOptions<ChromaVectorStoreOptions> options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(_options.CollectionName))
            throw new InvalidOperationException("ChromaVectorStore collection name must be configured.");
        if (string.IsNullOrWhiteSpace(_options.Tenant))
            throw new InvalidOperationException("ChromaVectorStore tenant must be configured.");
        if (string.IsNullOrWhiteSpace(_options.Database))
            throw new InvalidOperationException("ChromaVectorStore database must be configured.");
    }

    public async Task<IReadOnlyList<VectorMatch>> SearchAsync(IReadOnlyList<float> queryEmbedding, int topK, CancellationToken cancellationToken = default)
    {
        if (topK <= 0 || queryEmbedding.Count == 0)
            return [];

        var collectionId = await EnsureCollectionIdAsync(cancellationToken);
        var payload = new QueryRequestPayload([queryEmbedding], topK, ["embeddings", "distances"]);
        using var response = await _httpClient.PostAsJsonAsync(
            BuildCollectionQueryPath(collectionId),
            payload,
            JsonOptions,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var queryResponse = await response.Content.ReadFromJsonAsync<QueryResponse>(JsonOptions, cancellationToken);
        if (queryResponse?.Ids is null || queryResponse.Ids.Count == 0)
            return [];

        var ids = queryResponse.Ids[0];
        if (ids.Count == 0)
            return [];

        var embeddings = queryResponse.Embeddings is { Count: > 0 } ? queryResponse.Embeddings[0] : null;
        var distances = queryResponse.Distances is { Count: > 0 } ? queryResponse.Distances[0] : null;

        var matches = new List<VectorMatch>(ids.Count);
        for (var index = 0; index < ids.Count; index++)
        {
            var score = 0d;

            // Preserve previous LocalVectorStore behavior by using cosine similarity when vectors are available.
            if (embeddings is not null && index < embeddings.Count && embeddings[index] is { Count: > 0 } embedding)
            {
                score = CosineSimilarity.Calculate(queryEmbedding, embedding);
            }
            else if (distances is not null && index < distances.Count && distances[index] is double distance)
            {
                score = 1 - distance;
            }

            matches.Add(new VectorMatch(ids[index], score));
        }

        return matches
            .OrderByDescending(match => match.Score)
            .Take(topK)
            .ToList();
    }

    private async Task<string> EnsureCollectionIdAsync(CancellationToken cancellationToken)
    {
        if (_collectionId is not null)
            return _collectionId;

        await _collectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_collectionId is not null)
                return _collectionId;

            var payload = new CreateCollectionPayload(_options.CollectionName, true);
            using var response = await _httpClient.PostAsJsonAsync(BuildCollectionsPath(), payload, JsonOptions, cancellationToken);
            response.EnsureSuccessStatusCode();

            var collection = await response.Content.ReadFromJsonAsync<CollectionResponse>(JsonOptions, cancellationToken);
            if (string.IsNullOrWhiteSpace(collection?.Id))
                throw new InvalidOperationException("ChromaDB collection creation response did not include an id.");

            _collectionId = collection.Id;
            return _collectionId;
        }
        finally
        {
            _collectionLock.Release();
        }
    }

    private string BuildCollectionsPath()
    {
        return $"api/v2/tenants/{Escape(_options.Tenant)}/databases/{Escape(_options.Database)}/collections";
    }

    private string BuildCollectionQueryPath(string collectionId)
    {
        return $"{BuildCollectionsPath()}/{Escape(collectionId)}/query";
    }

    private static string Escape(string value) => Uri.EscapeDataString(value);

    private sealed record CreateCollectionPayload(
        string Name,
        [property: JsonPropertyName("get_or_create")] bool GetOrCreate);

    private sealed record CollectionResponse(string Id);

    private sealed record QueryRequestPayload(
        [property: JsonPropertyName("query_embeddings")] IReadOnlyList<IReadOnlyList<float>> QueryEmbeddings,
        [property: JsonPropertyName("n_results")] int NResults,
        [property: JsonPropertyName("include")] IReadOnlyList<string> Include);

    private sealed record QueryResponse(
        IReadOnlyList<IReadOnlyList<string>> Ids,
        IReadOnlyList<IReadOnlyList<IReadOnlyList<float>?>>? Embeddings,
        IReadOnlyList<IReadOnlyList<double?>>? Distances);
}