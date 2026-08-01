using Embedding.DotNet;
using RAG.DotNet;
using SemanticSearch.DotNet;
using VectorStores.DotNet;
using Xunit;

namespace Services.DotNet.UnitTests;

public class RagServiceTests
{
    [Fact]
    public async Task RetrievalService_ReturnsStructuredContextAndSources()
    {
        var service = new RetrievalService(
            new SemanticSearchService(new StubEmbeddingService(), new StubVectorStore(0.91), new StubMetadataProvider()),
            new StubMetadataProvider());

        var result = await service.RetrieveAsync("What spicy chicken meal can I cook?");

        var recipe = Assert.Single(result.Recipes);
        Assert.Equal("1", recipe.RecipeId);
        Assert.Equal("Spicy Chicken", recipe.Title);
        Assert.Equal("Freezer-friendly chicken dinner", recipe.Description);
        Assert.Equal(new[] { "chicken", "pepper" }, recipe.Ingredients);
        Assert.Equal("Slice chicken and season it", recipe.PreparationSteps);
        Assert.Equal("45", recipe.CookingTime);
        Assert.Equal(1.0 / 61.0, Assert.Single(result.Sources).SimilarityScore, 6);
    }

    [Fact]
    public async Task RetrievalService_FiltersLowSimilarityMatches()
    {
        var service = new RetrievalService(
            new SemanticSearchService(new StubEmbeddingService(), new StubVectorStore(0.01), new StubMetadataProvider()),
            new StubMetadataProvider(),
            minimumSimilarity: 0.2);

        var result = await service.RetrieveAsync("Unknown question");

        Assert.Empty(result.Recipes);
        Assert.Empty(result.Sources);
    }

    [Fact]
    public async Task RetrievalService_UsesRewrittenQueryForSemanticSearch()
    {
        var embeddingService = new RecordingEmbeddingService();
        var service = new RetrievalService(
            new SemanticSearchService(embeddingService, new StubVectorStore(0.91), new StubMetadataProvider()),
            new StubMetadataProvider(),
            new StubQueryRewriter("chicken freezer recipes"));

        await service.RetrieveAsync("What chicken recipes do you have?");

        Assert.Equal("chicken freezer recipes", embeddingService.LastText);
    }

    [Fact]
    public async Task RetrievalService_WhenRewriteReturnsEmpty_FallsBackToOriginalQuery()
    {
        var embeddingService = new RecordingEmbeddingService();
        var service = new RetrievalService(
            new SemanticSearchService(embeddingService, new StubVectorStore(0.91), new StubMetadataProvider()),
            new StubMetadataProvider(),
            new StubQueryRewriter("   "));

        await service.RetrieveAsync("What chicken recipes do you have?");

        Assert.Equal("What chicken recipes do you have?", embeddingService.LastText);
    }

    [Fact]
    public async Task RetrievalService_HybridSearch_FusesSemanticAndKeywordRankings()
    {
        var service = new RetrievalService(
            new SemanticSearchService(new StubEmbeddingService(), new MultiMatchVectorStore([
                new VectorMatch("1", 0.95),
                new VectorMatch("3", 0.70)
            ]), new MultiMetadataProvider()),
            new MultiMetadataProvider(),
            keywordSearchService: new StubKeywordSearchService([
                new KeywordSearchResult("2", 4),
                new KeywordSearchResult("1", 3)
            ]),
            topK: 3,
            minimumSimilarity: 0.2);

        var result = await service.RetrieveAsync("chicken dinner ideas");

        Assert.Equal(3, result.Recipes.Count);
        Assert.Equal(new[] { "1", "2", "3" }, result.Recipes.Select(recipe => recipe.RecipeId).ToArray());
        Assert.True(result.Recipes[0].SimilarityScore > result.Recipes[1].SimilarityScore);
        Assert.True(result.Recipes[1].SimilarityScore > result.Recipes[2].SimilarityScore);
    }

    [Fact]
    public async Task RetrievalService_HybridSearch_ReturnsKeywordResultsWhenSemanticFilteredOut()
    {
        var service = new RetrievalService(
            new SemanticSearchService(new StubEmbeddingService(), new MultiMatchVectorStore([
                new VectorMatch("1", 0.01)
            ]), new MultiMetadataProvider()),
            new MultiMetadataProvider(),
            keywordSearchService: new StubKeywordSearchService([
                new KeywordSearchResult("2", 2)
            ]),
            topK: 3,
            minimumSimilarity: 0.2);

        var result = await service.RetrieveAsync("beef dinner");

        var recipe = Assert.Single(result.Recipes);
        Assert.Equal("2", recipe.RecipeId);
    }

    [Fact]
    public async Task RetrievalService_Reranking_ReordersCandidatesAndPreservesMetadata()
    {
        var reranker = new StubReranker(["2", "1", "3"]);
        var service = new RetrievalService(
            new SemanticSearchService(new StubEmbeddingService(), new MultiMatchVectorStore([
                new VectorMatch("1", 0.95),
                new VectorMatch("3", 0.70)
            ]), new MultiMetadataProvider()),
            new MultiMetadataProvider(),
            keywordSearchService: new StubKeywordSearchService([
                new KeywordSearchResult("2", 4),
                new KeywordSearchResult("1", 3)
            ]),
            reranker: reranker,
            topK: 3,
            minimumSimilarity: 0.2);

        var result = await service.RetrieveAsync("chicken dinner ideas");

        Assert.Equal("chicken dinner ideas", reranker.LastQuery);
        Assert.Equal(new[] { "1", "2", "3" }, reranker.LastCandidateIds);
        Assert.Equal(new[] { "2", "1", "3" }, result.Recipes.Select(recipe => recipe.RecipeId).ToArray());
        Assert.Equal("Beef Stir Fry", result.Recipes[0].Title);
        Assert.Equal("Beef dinner", result.Recipes[0].Description);
    }

    [Fact]
    public async Task RetrievalService_WhenRerankingFails_PreservesOriginalRanking()
    {
        var service = new RetrievalService(
            new SemanticSearchService(new StubEmbeddingService(), new MultiMatchVectorStore([
                new VectorMatch("1", 0.95),
                new VectorMatch("3", 0.70)
            ]), new MultiMetadataProvider()),
            new MultiMetadataProvider(),
            keywordSearchService: new StubKeywordSearchService([
                new KeywordSearchResult("2", 4),
                new KeywordSearchResult("1", 3)
            ]),
            reranker: new ThrowingReranker(),
            topK: 3,
            minimumSimilarity: 0.2);

        var result = await service.RetrieveAsync("chicken dinner ideas");

        Assert.Equal(new[] { "1", "2", "3" }, result.Recipes.Select(recipe => recipe.RecipeId).ToArray());
    }

    [Fact]
    public void PromptBuilder_RendersRepositoryContext()
    {
        var builder = new PromptBuilder("Context:\n{recipes}\nQuestion:\n{question}");
        var recipe = new RetrievalRecipe("1", "Spicy Chicken", "Dinner", "spicy", ["chicken"], "Slice", "45", 0.91);

        var prompt = builder.Build("What can I cook?", [recipe]);

        Assert.Contains("Recipe ID: 1", prompt);
        Assert.Contains("Ingredients: chicken", prompt);
        Assert.Contains("Similarity score: 0.910000", prompt);
        Assert.Contains("Question:\nWhat can I cook?", prompt);
    }

    private sealed class StubEmbeddingService : IEmbeddingService
    {
        public Task<EmbeddingResponse> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default) =>
            Task.FromResult(new EmbeddingResponse("test", 2, [1f, 0f]));
    }

    private sealed class RecordingEmbeddingService : IEmbeddingService
    {
        public string? LastText { get; private set; }

        public Task<EmbeddingResponse> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            LastText = text;
            return Task.FromResult(new EmbeddingResponse("test", 2, [1f, 0f]));
        }
    }

    private sealed class StubQueryRewriter : IQueryRewriter
    {
        private readonly string _rewritten;

        public StubQueryRewriter(string rewritten)
        {
            _rewritten = rewritten;
        }

        public Task<string> RewriteAsync(string query, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_rewritten);
        }
    }

    private sealed class StubVectorStore : IVectorStore
    {
        private readonly double _score;

        public StubVectorStore(double score)
        {
            _score = score;
        }

        public Task<IReadOnlyList<VectorMatch>> SearchAsync(IReadOnlyList<float> queryEmbedding, int topK, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<VectorMatch>>([new VectorMatch("1", _score)]);

        public Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpsertAsync(IReadOnlyList<VectorDocument> documents, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteAsync(IReadOnlyList<string> recipeIds, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ClearCollectionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class MultiMatchVectorStore : IVectorStore
    {
        private readonly IReadOnlyList<VectorMatch> _matches;

        public MultiMatchVectorStore(IReadOnlyList<VectorMatch> matches)
        {
            _matches = matches;
        }

        public Task<IReadOnlyList<VectorMatch>> SearchAsync(IReadOnlyList<float> queryEmbedding, int topK, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<VectorMatch>>(_matches.Take(topK).ToList());
        }

        public Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpsertAsync(IReadOnlyList<VectorDocument> documents, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteAsync(IReadOnlyList<string> recipeIds, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ClearCollectionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubMetadataProvider : ISemanticRecipeMetadataProvider
    {
        public Task<RecipeMetadata> GetMetadataAsync(string recipeId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RecipeMetadata(
                recipeId,
                "Spicy Chicken",
                "spicy chicken dinner",
                "Freezer-friendly chicken dinner",
                "spicy, chicken",
                ["chicken", "pepper"],
                "Slice chicken and season it",
                "45"));
    }

    private sealed class MultiMetadataProvider : ISemanticRecipeMetadataProvider
    {
        public Task<RecipeMetadata> GetMetadataAsync(string recipeId, CancellationToken cancellationToken = default)
        {
            var metadata = recipeId switch
            {
                "1" => new RecipeMetadata("1", "Spicy Chicken", "spicy chicken", "Spicy chicken dinner", "spicy", ["chicken"], "Cook", "45"),
                "2" => new RecipeMetadata("2", "Beef Stir Fry", "beef stir fry", "Beef dinner", "beef", ["beef"], "Stir fry", "30"),
                "3" => new RecipeMetadata("3", "Garlic Rice", "garlic rice", "Rice side", "rice", ["rice"], "Boil", "20"),
                _ => new RecipeMetadata(recipeId, $"Recipe {recipeId}", string.Empty)
            };

            return Task.FromResult(metadata);
        }
    }

    private sealed class StubKeywordSearchService : IKeywordSearchService
    {
        private readonly IReadOnlyList<KeywordSearchResult> _results;

        public StubKeywordSearchService(IReadOnlyList<KeywordSearchResult> results)
        {
            _results = results;
        }

        public Task<IReadOnlyList<KeywordSearchResult>> SearchAsync(string query, int topK, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<KeywordSearchResult>>(_results.Take(topK).ToList());
        }
    }

    private sealed class StubReranker : IReranker
    {
        private readonly IReadOnlyList<string> _order;

        public StubReranker(IReadOnlyList<string> order)
        {
            _order = order;
        }

        public string? LastQuery { get; private set; }

        public IReadOnlyList<string> LastCandidateIds { get; private set; } = [];

        public Task<IReadOnlyList<RetrievalRecipe>> RerankAsync(string query, IReadOnlyList<RetrievalRecipe> candidates, CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            LastCandidateIds = candidates.Select(candidate => candidate.RecipeId).ToList();

            var byId = candidates.ToDictionary(candidate => candidate.RecipeId, StringComparer.Ordinal);
            var ordered = _order
                .Where(recipeId => byId.ContainsKey(recipeId))
                .Select(recipeId => byId[recipeId])
                .ToList();

            return Task.FromResult<IReadOnlyList<RetrievalRecipe>>(ordered);
        }
    }

    private sealed class ThrowingReranker : IReranker
    {
        public Task<IReadOnlyList<RetrievalRecipe>> RerankAsync(string query, IReadOnlyList<RetrievalRecipe> candidates, CancellationToken cancellationToken = default)
        {
            throw new TimeoutException("rerank timeout");
        }
    }
}
