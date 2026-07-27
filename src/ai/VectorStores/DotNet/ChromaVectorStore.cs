using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;
using System.Globalization;

namespace VectorStores.DotNet;

public sealed class ChromaVectorStore : IVectorStore
{
    public const string HttpClientName = "ChromaVectorStore";

    private static readonly ActivitySource ActivitySource = new("FreezerLegoMeals.AI");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ChromaVectorStoreOptions _options;
    private readonly ILogger<ChromaVectorStore> _logger;
    private readonly SemaphoreSlim _collectionLock = new(1, 1);
    private string? _collectionId;

    public ChromaVectorStore(HttpClient httpClient, IOptions<ChromaVectorStoreOptions> options, ILogger<ChromaVectorStore>? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? NullLogger<ChromaVectorStore>.Instance;

        if (string.IsNullOrWhiteSpace(_options.CollectionName))
            throw new InvalidOperationException("ChromaVectorStore collection name must be configured.");
        if (string.IsNullOrWhiteSpace(_options.Tenant))
            throw new InvalidOperationException("ChromaVectorStore tenant must be configured.");
        if (string.IsNullOrWhiteSpace(_options.Database))
            throw new InvalidOperationException("ChromaVectorStore database must be configured.");
    }

    public async Task<IReadOnlyList<VectorMatch>> SearchAsync(IReadOnlyList<float> queryEmbedding, int topK, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("vector-store.chroma.search", ActivityKind.Client);
        activity?.SetTag("vector_store.backend", "chroma");
        activity?.SetTag("vector_store.collection", _options.CollectionName);
        activity?.SetTag("vector_store.top_k", topK);
        activity?.SetTag("vector_store.query_dimensions", queryEmbedding.Count);

        var startedAt = Stopwatch.StartNew();
        if (topK <= 0 || queryEmbedding.Count == 0)
        {
            _logger.LogInformation(
                "Vector search skipped collection={CollectionName} topK={TopK} dimensions={Dimensions} reason={Reason}",
                _options.CollectionName,
                topK,
                queryEmbedding.Count,
                topK <= 0 ? "invalid-topk" : "empty-embedding");
            activity?.SetTag("vector_store.result_count", 0);
            activity?.SetTag("vector_store.decision_reason", topK <= 0 ? "invalid-topk" : "empty-embedding");
            return [];
        }

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
        {
            startedAt.Stop();
            LogQueryDiagnostics(collectionId, topK, 0, 0, 0, startedAt.Elapsed.TotalMilliseconds, "no-ids-array");
            activity?.SetTag("vector_store.result_count", 0);
            activity?.SetTag("vector_store.decision_reason", "no-ids-array");
            activity?.SetTag("vector_store.latency_ms", startedAt.Elapsed.TotalMilliseconds);
            return [];
        }

        var ids = queryResponse.Ids[0];
        if (ids.Count == 0)
        {
            startedAt.Stop();
            LogQueryDiagnostics(collectionId, topK, 0, 0, 0, startedAt.Elapsed.TotalMilliseconds, "empty-id-list");
            activity?.SetTag("vector_store.result_count", 0);
            activity?.SetTag("vector_store.decision_reason", "empty-id-list");
            activity?.SetTag("vector_store.latency_ms", startedAt.Elapsed.TotalMilliseconds);
            return [];
        }

        var embeddings = queryResponse.Embeddings is { Count: > 0 } ? queryResponse.Embeddings[0] : null;
        var distances = queryResponse.Distances is { Count: > 0 } ? queryResponse.Distances[0] : null;
        var usedEmbeddingScoreCount = 0;
        var usedDistanceFallbackCount = 0;

        var matches = new List<VectorMatch>(ids.Count);
        for (var index = 0; index < ids.Count; index++)
        {
            var score = 0d;

            // Preserve previous LocalVectorStore behavior by using cosine similarity when vectors are available.
            if (embeddings is not null && index < embeddings.Count && embeddings[index] is { Count: > 0 } embedding)
            {
                score = CosineSimilarity.Calculate(queryEmbedding, embedding);
                usedEmbeddingScoreCount++;
            }
            else if (distances is not null && index < distances.Count && distances[index] is double distance)
            {
                score = 1 - distance;
                usedDistanceFallbackCount++;
            }

            matches.Add(new VectorMatch(ids[index], score));
        }

        var ranked = matches
            .OrderByDescending(match => match.Score)
            .Take(topK)
            .ToList();

        startedAt.Stop();
        LogQueryDiagnostics(
            collectionId,
            topK,
            ids.Count,
            usedEmbeddingScoreCount,
            usedDistanceFallbackCount,
            startedAt.Elapsed.TotalMilliseconds,
            "ranked");
        activity?.SetTag("vector_store.raw_match_count", ids.Count);
        activity?.SetTag("vector_store.result_count", ranked.Count);
        activity?.SetTag("vector_store.embedding_score_count", usedEmbeddingScoreCount);
        activity?.SetTag("vector_store.distance_fallback_count", usedDistanceFallbackCount);
        activity?.SetTag("vector_store.latency_ms", startedAt.Elapsed.TotalMilliseconds);

        return ranked;
    }

    private async Task<string> EnsureCollectionIdAsync(CancellationToken cancellationToken)
    {
        if (_collectionId is not null)
            return _collectionId;

        await _collectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_collectionId is not null)
            {
                _logger.LogDebug("Chroma collection cache hit collectionName={CollectionName} collectionId={CollectionId}", _options.CollectionName, _collectionId);
                return _collectionId;
            }

            var payload = new CreateCollectionPayload(_options.CollectionName, true);
            using var response = await _httpClient.PostAsJsonAsync(BuildCollectionsPath(), payload, JsonOptions, cancellationToken);
            response.EnsureSuccessStatusCode();

            var collection = await response.Content.ReadFromJsonAsync<CollectionResponse>(JsonOptions, cancellationToken);
            if (string.IsNullOrWhiteSpace(collection?.Id))
                throw new InvalidOperationException("ChromaDB collection creation response did not include an id.");

            _collectionId = collection.Id;
            _logger.LogInformation(
                "Chroma collection resolved collectionName={CollectionName} collectionId={CollectionId} tenant={Tenant} database={Database}",
                _options.CollectionName,
                _collectionId,
                _options.Tenant,
                _options.Database);
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

    private void LogQueryDiagnostics(
        string collectionId,
        int requestedTopK,
        int rawMatchCount,
        int embeddingScoreCount,
        int distanceFallbackCount,
        double latencyMs,
        string decision)
    {
        _logger.LogInformation(
            "Vector search diagnostics backend={Backend} collection={CollectionName} collectionId={CollectionId} requestedTopK={RequestedTopK} rawMatchCount={RawMatchCount} embeddingScoreCount={EmbeddingScoreCount} distanceFallbackCount={DistanceFallbackCount} decision={Decision} latencyMs={LatencyMs}",
            "chroma",
            _options.CollectionName,
            collectionId,
            requestedTopK,
            rawMatchCount,
            embeddingScoreCount,
            distanceFallbackCount,
            decision,
            latencyMs.ToString("F3", CultureInfo.InvariantCulture));
    }

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