using Embedding.DotNet;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;
using VectorStores.DotNet;

namespace SemanticSearch.DotNet;

public sealed class SemanticSearchService
{
    private static readonly ActivitySource ActivitySource = new("FreezerLegoMeals.AI");
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStore _vectorStore;
    private readonly ISemanticRecipeMetadataProvider _metadataProvider;
    private readonly ILogger<SemanticSearchService> _logger;

    public SemanticSearchService(
        IEmbeddingService embeddingService,
        IVectorStore vectorStore,
        ISemanticRecipeMetadataProvider metadataProvider,
        ILogger<SemanticSearchService>? logger = null)
    {
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        _metadataProvider = metadataProvider ?? throw new ArgumentNullException(nameof(metadataProvider));
        _logger = logger ?? NullLogger<SemanticSearchService>.Instance;
    }

    public async Task<IReadOnlyList<SemanticSearchResult>> SearchAsync(string query, int topK = 5, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("semantic-search.search", ActivityKind.Internal);
        activity?.SetTag("semantic_search.query_length", query?.Length ?? 0);
        activity?.SetTag("semantic_search.top_k", topK);
        var startedAt = Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(query) || topK <= 0)
        {
            _logger.LogInformation(
                "Semantic search skipped queryLength={QueryLength} topK={TopK} reason={Reason}",
                query?.Length ?? 0,
                topK,
                string.IsNullOrWhiteSpace(query) ? "empty-query" : "invalid-topk");
            activity?.SetTag("semantic_search.result_count", 0);
            activity?.SetTag("semantic_search.reason", string.IsNullOrWhiteSpace(query) ? "empty-query" : "invalid-topk");
            return [];
        }

        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query, cancellationToken);
        var matches = await _vectorStore.SearchAsync(queryEmbedding.Embedding, topK, cancellationToken);
        var results = new List<SemanticSearchResult>();
        foreach (var match in matches)
        {
            var metadata = await _metadataProvider.GetMetadataAsync(match.RecipeId, cancellationToken);
            results.Add(new SemanticSearchResult(
                match.RecipeId,
                metadata.Title,
                Math.Round(match.Score, 6),
                metadata.MatchedText,
                $"High semantic similarity between the query and {metadata.Title}."));
        }

        startedAt.Stop();
        _logger.LogInformation(
            "Semantic search completed queryLength={QueryLength} topK={TopK} vectorMatches={VectorMatches} enrichedResults={EnrichedResults} latencyMs={LatencyMs}",
            query.Length,
            topK,
            matches.Count,
            results.Count,
            startedAt.Elapsed.TotalMilliseconds);
        activity?.SetTag("semantic_search.vector_match_count", matches.Count);
        activity?.SetTag("semantic_search.result_count", results.Count);
        activity?.SetTag("semantic_search.latency_ms", startedAt.Elapsed.TotalMilliseconds);

        return results;
    }
}