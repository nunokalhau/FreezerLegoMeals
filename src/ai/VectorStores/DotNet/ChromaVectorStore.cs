using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;
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

    public async Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        var collectionId = await EnsureCollectionIdAsync(cancellationToken);
        _logger.LogInformation(
            "Vector collection selected backend={Backend} collection={CollectionName} collectionId={CollectionId} tenant={Tenant} database={Database}",
            "chroma",
            _options.CollectionName,
            collectionId,
            _options.Tenant,
            _options.Database);
    }

    public async Task UpsertAsync(IReadOnlyList<VectorDocument> documents, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documents);

        if (documents.Count == 0)
        {
            _logger.LogInformation(
                "Vector upsert skipped collection={CollectionName} reason={Reason}",
                _options.CollectionName,
                "no-documents");
            return;
        }

        if (documents.Any(document => string.IsNullOrWhiteSpace(document.RecipeId)))
            throw new ArgumentException("Each vector document must include a recipe id.", nameof(documents));

        if (documents.Any(document => document.Embedding.Count == 0))
            throw new ArgumentException("Each vector document must include an embedding.", nameof(documents));

        using var activity = ActivitySource.StartActivity("vector-store.chroma.upsert", ActivityKind.Client);
        activity?.SetTag("vector_store.backend", "chroma");
        activity?.SetTag("vector_store.collection", _options.CollectionName);
        activity?.SetTag("vector_store.document_count", documents.Count);

        var startedAt = Stopwatch.StartNew();
        var collectionId = await EnsureCollectionIdAsync(cancellationToken);
        var ids = documents.Select(document => document.RecipeId).ToList();
        var embeddings = documents.Select(document => document.Embedding).ToList();
        var payload = BuildUpsertPayload(ids, embeddings, documents);

        var existingIds = await GetExistingDocumentIdsAsync(collectionId, ids, cancellationToken);
        var updatedCount = ids.Count(id => existingIds.Contains(id));
        var insertedCount = ids.Count - updatedCount;

        using var response = await _httpClient.PostAsJsonAsync(
            BuildCollectionUpsertPath(collectionId),
            payload,
            JsonOptions,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalVectors = await TryGetVectorCountAsync(collectionId, cancellationToken);

        startedAt.Stop();
        _logger.LogInformation(
            "Vector upsert completed backend={Backend} collection={CollectionName} collectionId={CollectionId} documentsInserted={DocumentsInserted} documentsUpdated={DocumentsUpdated} documentsSkipped={DocumentsSkipped} totalVectors={TotalVectors} latencyMs={LatencyMs}",
            "chroma",
            _options.CollectionName,
            collectionId,
            insertedCount,
            updatedCount,
            0,
            totalVectors,
            startedAt.Elapsed.TotalMilliseconds);
        activity?.SetTag("vector_store.collection_id", collectionId);
        activity?.SetTag("vector_store.documents_inserted", insertedCount);
        activity?.SetTag("vector_store.documents_updated", updatedCount);
        if (totalVectors.HasValue)
        {
            activity?.SetTag("vector_store.total_vectors", totalVectors.Value);
        }
        activity?.SetTag("vector_store.latency_ms", startedAt.Elapsed.TotalMilliseconds);
    }

    public async Task DeleteAsync(IReadOnlyList<string> recipeIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recipeIds);

        if (recipeIds.Count == 0)
        {
            _logger.LogInformation(
                "Vector delete skipped collection={CollectionName} reason={Reason}",
                _options.CollectionName,
                "no-recipe-ids");
            return;
        }

        if (recipeIds.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Recipe ids must be non-empty values.", nameof(recipeIds));

        using var activity = ActivitySource.StartActivity("vector-store.chroma.delete", ActivityKind.Client);
        activity?.SetTag("vector_store.backend", "chroma");
        activity?.SetTag("vector_store.collection", _options.CollectionName);
        activity?.SetTag("vector_store.delete_count", recipeIds.Count);

        var startedAt = Stopwatch.StartNew();
        var collectionId = await EnsureCollectionIdAsync(cancellationToken);
        var payload = new DeleteRequestPayload(recipeIds);
        using var response = await _httpClient.PostAsJsonAsync(
            BuildCollectionDeletePath(collectionId),
            payload,
            JsonOptions,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        startedAt.Stop();
        _logger.LogInformation(
            "Vector delete completed backend={Backend} collection={CollectionName} collectionId={CollectionId} deletedCount={DeletedCount} latencyMs={LatencyMs}",
            "chroma",
            _options.CollectionName,
            collectionId,
            recipeIds.Count,
            startedAt.Elapsed.TotalMilliseconds);
        activity?.SetTag("vector_store.collection_id", collectionId);
        activity?.SetTag("vector_store.latency_ms", startedAt.Elapsed.TotalMilliseconds);
    }

    public async Task ClearCollectionAsync(CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("vector-store.chroma.clear", ActivityKind.Client);
        activity?.SetTag("vector_store.backend", "chroma");
        activity?.SetTag("vector_store.collection", _options.CollectionName);

        var startedAt = Stopwatch.StartNew();
        var collectionId = await EnsureCollectionIdAsync(cancellationToken);
        var deletedCount = await ClearCollectionDocumentsAsync(collectionId, cancellationToken);
        var totalVectors = await TryGetVectorCountAsync(collectionId, cancellationToken);
        startedAt.Stop();
        _logger.LogInformation(
            "Vector collection cleared backend={Backend} collection={CollectionName} collectionId={CollectionId} documentsDeleted={DocumentsDeleted} totalVectors={TotalVectors} latencyMs={LatencyMs}",
            "chroma",
            _options.CollectionName,
            collectionId,
            deletedCount,
            totalVectors,
            startedAt.Elapsed.TotalMilliseconds);
        activity?.SetTag("vector_store.collection_id", collectionId);
        activity?.SetTag("vector_store.deleted_count", deletedCount);
        if (totalVectors.HasValue)
        {
            activity?.SetTag("vector_store.total_vectors", totalVectors.Value);
        }
        activity?.SetTag("vector_store.latency_ms", startedAt.Elapsed.TotalMilliseconds);
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

        try
        {
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
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            startedAt.Stop();
            _logger.LogWarning(
                exception,
                "Vector store dependency failure backend={Backend} collection={CollectionName} topK={TopK} latencyMs={LatencyMs}",
                "chroma",
                _options.CollectionName,
                topK,
                startedAt.Elapsed.TotalMilliseconds);
            activity?.SetTag("vector_store.failure", exception.GetType().Name);
            activity?.SetTag("vector_store.latency_ms", startedAt.Elapsed.TotalMilliseconds);
            throw;
        }
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

            var existingCollectionId = await TryGetCollectionIdByNameAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(existingCollectionId))
            {
                _collectionId = existingCollectionId;
                _logger.LogInformation(
                    "Chroma collection resolved collectionName={CollectionName} collectionId={CollectionId} tenant={Tenant} database={Database} existed={Existed}",
                    _options.CollectionName,
                    _collectionId,
                    _options.Tenant,
                    _options.Database,
                    true);
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
                "Chroma collection resolved collectionName={CollectionName} collectionId={CollectionId} tenant={Tenant} database={Database} existed={Existed}",
                _options.CollectionName,
                _collectionId,
                _options.Tenant,
                _options.Database,
                false);
            return _collectionId;
        }
        finally
        {
            _collectionLock.Release();
        }
    }

    private async Task<string?> TryGetCollectionIdByNameAsync(CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(BuildCollectionsPath(), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (!item.TryGetProperty("name", out var nameProperty)
                    || !item.TryGetProperty("id", out var idProperty))
                {
                    continue;
                }

                var name = nameProperty.GetString();
                if (string.Equals(name, _options.CollectionName, StringComparison.Ordinal))
                {
                    return idProperty.GetString();
                }
            }

            return null;
        }

        if (document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty("name", out var singleNameProperty)
            && document.RootElement.TryGetProperty("id", out var singleIdProperty)
            && string.Equals(singleNameProperty.GetString(), _options.CollectionName, StringComparison.Ordinal))
        {
            return singleIdProperty.GetString();
        }

        return null;
    }

    private string BuildCollectionsPath()
    {
        return $"api/v2/tenants/{Escape(_options.Tenant)}/databases/{Escape(_options.Database)}/collections";
    }

    private string BuildCollectionQueryPath(string collectionId)
    {
        return $"{BuildCollectionsPath()}/{Escape(collectionId)}/query";
    }

    private string BuildCollectionUpsertPath(string collectionId)
    {
        return $"{BuildCollectionsPath()}/{Escape(collectionId)}/upsert";
    }

    private string BuildCollectionDeletePath(string collectionId)
    {
        return $"{BuildCollectionsPath()}/{Escape(collectionId)}/delete";
    }

    private string BuildCollectionGetPath(string collectionId)
    {
        return $"{BuildCollectionsPath()}/{Escape(collectionId)}/get";
    }

    private string BuildCollectionCountPath(string collectionId)
    {
        return $"{BuildCollectionsPath()}/{Escape(collectionId)}/count";
    }

    private async Task<int> ClearCollectionDocumentsAsync(string collectionId, CancellationToken cancellationToken)
    {
        const int pageSize = 512;
        var deletedCount = 0;
        while (true)
        {
            using var getResponse = await _httpClient.PostAsJsonAsync(
                BuildCollectionGetPath(collectionId),
                new GetRequestPayload(pageSize, 0, ["documents"]),
                JsonOptions,
                cancellationToken);
            getResponse.EnsureSuccessStatusCode();

            var payload = await getResponse.Content.ReadFromJsonAsync<GetResponse>(JsonOptions, cancellationToken);
            var ids = payload?.Ids ?? [];
            if (ids.Count == 0)
                break;

            deletedCount += ids.Count;

            using var deleteResponse = await _httpClient.PostAsJsonAsync(
                BuildCollectionDeletePath(collectionId),
                new DeleteRequestPayload(ids),
                JsonOptions,
                cancellationToken);
            deleteResponse.EnsureSuccessStatusCode();
        }

        return deletedCount;
    }

    private async Task<HashSet<string>> GetExistingDocumentIdsAsync(
        string collectionId,
        IReadOnlyList<string> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                BuildCollectionGetPath(collectionId),
                new ExistingIdsGetRequestPayload(ids, ["metadatas"]),
                JsonOptions,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new HashSet<string>(StringComparer.Ordinal);
            }

            var payload = await response.Content.ReadFromJsonAsync<GetResponse>(JsonOptions, cancellationToken);
            return (payload?.Ids ?? [])
                .ToHashSet(StringComparer.Ordinal);
        }
        catch
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }
    }

    private async Task<int?> TryGetVectorCountAsync(string collectionId, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                BuildCollectionCountPath(collectionId),
                new CountRequestPayload(),
                JsonOptions,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<CountResponse>(JsonOptions, cancellationToken);
            return payload?.Count;
        }
        catch
        {
            return null;
        }
    }

    private static UpsertRequestPayload BuildUpsertPayload(
        IReadOnlyList<string> ids,
        IReadOnlyList<IReadOnlyList<float>> embeddings,
        IReadOnlyList<VectorDocument> documents)
    {
        var includeDocuments = documents.All(document => !string.IsNullOrWhiteSpace(document.Document));
        var includeMetadata = documents.All(document => document.Metadata is not null);

        var documentsPayload = includeDocuments
            ? documents.Select(document => document.Document!).ToList()
            : null;

        var metadataPayload = includeMetadata
            ? documents.Select(document => document.Metadata!).ToList()
            : null;

        return new UpsertRequestPayload(ids, embeddings, documentsPayload, metadataPayload);
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

    private sealed record UpsertRequestPayload(
        [property: JsonPropertyName("ids")] IReadOnlyList<string> Ids,
        [property: JsonPropertyName("embeddings")] IReadOnlyList<IReadOnlyList<float>> Embeddings,
        [property: JsonPropertyName("documents")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        IReadOnlyList<string>? Documents,
        [property: JsonPropertyName("metadatas")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        IReadOnlyList<IReadOnlyDictionary<string, object?>>? Metadatas);

    private sealed record DeleteRequestPayload(
        [property: JsonPropertyName("ids")] IReadOnlyList<string> Ids);

    private sealed record ExistingIdsGetRequestPayload(
        [property: JsonPropertyName("ids")] IReadOnlyList<string> Ids,
        [property: JsonPropertyName("include")] IReadOnlyList<string> Include);

    private sealed record GetRequestPayload(
        [property: JsonPropertyName("limit")] int Limit,
        [property: JsonPropertyName("offset")] int Offset,
        [property: JsonPropertyName("include")] IReadOnlyList<string> Include);

    private sealed record CountRequestPayload();

    private sealed record GetResponse(
        [property: JsonPropertyName("ids")] IReadOnlyList<string> Ids);

    private sealed record CountResponse(
        [property: JsonPropertyName("count")] int Count);

    private sealed record QueryResponse(
        IReadOnlyList<IReadOnlyList<string>> Ids,
        IReadOnlyList<IReadOnlyList<IReadOnlyList<float>?>>? Embeddings,
        IReadOnlyList<IReadOnlyList<double?>>? Distances);
}