using Embedding.DotNet;
using VectorStores.DotNet;

namespace SemanticSearch.DotNet;

public sealed class SemanticSearchService
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStore _vectorStore;
    private readonly ISemanticRecipeMetadataProvider _metadataProvider;

    public SemanticSearchService(
        IEmbeddingService embeddingService,
        IVectorStore vectorStore,
        ISemanticRecipeMetadataProvider metadataProvider)
    {
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        _metadataProvider = metadataProvider ?? throw new ArgumentNullException(nameof(metadataProvider));
    }

    public async Task<IReadOnlyList<SemanticSearchResult>> SearchAsync(string query, int topK = 5, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || topK <= 0)
            return [];

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

        return results;
    }
}