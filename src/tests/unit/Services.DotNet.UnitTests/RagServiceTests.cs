using Embedding.DotNet;
using Domain.DotNet;
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
        Assert.Equal("1", recipe.CanonicalRecipeId);
        Assert.Equal("Spicy Chicken", recipe.Title);
        Assert.Equal("Freezer-friendly chicken dinner", recipe.Description);
        Assert.Equal(new[] { "chicken", "pepper" }, recipe.Ingredients);
        Assert.Equal("Slice chicken and season it", recipe.PreparationSteps);
        Assert.Equal("45", recipe.CookingTime);
        Assert.Equal("canonical-multilingual-projection", recipe.RetrievalProfileId);
        Assert.Equal("canonical-multilingual-projection", result.Profile?.ProfileId);
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

        Assert.Equal("chicken freezer recipe", embeddingService.LastText);
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

        Assert.Equal("what chicken recipe do you have", embeddingService.LastText);
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
        Assert.Equal(RetrievalProfileFamily.CanonicalMultilingualProjection, result.Profile?.ProfileFamily);
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
        Assert.Equal("2", recipe.CanonicalRecipeId);
    }

    [Fact]
    public async Task RetrievalService_SelectsPerLanguageProfile_WhenStrictLocalizationEnabled()
    {
        var service = new RetrievalService(
            new SemanticSearchService(new StubEmbeddingService(), new StubVectorStore(0.91), new StubMetadataProvider()),
            new StubMetadataProvider());

        var result = await service.RetrieveAsync(
            "What spicy chicken meal can I cook?",
            LocalizationOptions.Create("pt", ["en"], strictMode: true));

        Assert.Equal("per-language-projection", result.Profile?.ProfileId);
        Assert.Equal(RetrievalProfileFamily.PerLanguageProjection, result.Profile?.ProfileFamily);
    }

    [Fact]
    public async Task RetrievalService_SelectsHybridProfile_WhenPreferredLanguageIsNonDefault()
    {
        var service = new RetrievalService(
            new SemanticSearchService(new StubEmbeddingService(), new StubVectorStore(0.91), new StubMetadataProvider()),
            new StubMetadataProvider());

        var result = await service.RetrieveAsync(
            "What spicy chicken meal can I cook?",
            LocalizationOptions.Create("pt", ["en"], strictMode: false));

        Assert.Equal("hybrid-precision-recall", result.Profile?.ProfileId);
        Assert.Equal(RetrievalProfileFamily.HybridPrecisionRecall, result.Profile?.ProfileFamily);
    }

    [Fact]
    public async Task RetrievalService_StrictLocalization_DropsCandidates_WhenLocalizedMetadataMissing()
    {
        var metadataProvider = new StrictAwareLocalizedMetadataProvider();
        var service = new RetrievalService(
            new SemanticSearchService(new StubEmbeddingService(), new StubVectorStore(0.91), metadataProvider),
            metadataProvider);

        var result = await service.RetrieveAsync(
            "Que receitas tens com frango?",
            LocalizationOptions.Create("pt", ["en"], strictMode: true));

        Assert.Empty(result.Recipes);
        Assert.Empty(result.Sources);
    }

    [Fact]
    public async Task RetrievalService_CanonicalCollapse_PreservesCanonicalIdentityAcrossProfiles()
    {
        var metadataProvider = new MultiMetadataProvider();
        var service = new RetrievalService(
            new SemanticSearchService(new StubEmbeddingService(), new MultiMatchVectorStore([
                new VectorMatch("1", 0.95),
                new VectorMatch("1#pt", 0.93),
                new VectorMatch("2", 0.70)
            ]), metadataProvider),
            metadataProvider,
            keywordSearchService: new StubKeywordSearchService([
                new KeywordSearchResult("1#pt", 4),
                new KeywordSearchResult("2", 3)
            ]),
            topK: 5,
            minimumSimilarity: 0.2);

        var canonicalProfileResult = await service.RetrieveAsync(
            "chicken dinner ideas",
            LocalizationOptions.Create("en", strictMode: false));
        var hybridProfileResult = await service.RetrieveAsync(
            "chicken dinner ideas",
            LocalizationOptions.Create("pt", ["en"], strictMode: false));

        Assert.Equal(new[] { "1", "2" }, canonicalProfileResult.Recipes.Select(recipe => recipe.CanonicalRecipeId).OrderBy(value => value, StringComparer.Ordinal).ToArray());
        Assert.Equal(new[] { "1", "2" }, hybridProfileResult.Recipes.Select(recipe => recipe.CanonicalRecipeId).OrderBy(value => value, StringComparer.Ordinal).ToArray());
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
    public void PromptBuilder_RendersRepositoryContext_AndLocalizationRules_ForPortuguese()
    {
        var builder = new PromptBuilder();
        var recipe = new RetrievalRecipe("1", "1", "Frango Salsa Verde", "Jantar", "frango, rapido", ["frango", "coentro"], "Assar", "45", 0.91, "per-language-projection");

        var prompt = builder.Build(
            "Que receitas tens com frango?",
            [recipe],
            "RecipeDiscovery",
            LocalizationOptions.Create("pt", ["en"], strictMode: true),
            requestedLanguage: "pt");

        Assert.Contains("LOCALIZATION RULES", prompt);
        Assert.Contains("Requested language: pt", prompt);
        Assert.Contains("Resolved language: pt", prompt);
        Assert.Contains("Strict mode: True", prompt);
        Assert.Contains("Fallback language: en", prompt);
        Assert.Contains("Never mix languages in the same answer.", prompt);
        Assert.Contains("Never translate localized recipe names.", prompt);
        Assert.Contains("Never translate localized ingredient names.", prompt);
        Assert.Contains("Never invent translations.", prompt);
        Assert.Contains("If localized information is unavailable for the resolved language, explicitly state that no localized result exists.", prompt);
        Assert.Contains("Strict mode is enabled: never answer using another language.", prompt);
        Assert.Contains("Recipe ID: 1", prompt);
        Assert.Contains("Title: Frango Salsa Verde", prompt);
        Assert.Contains("Ingredients: frango, coentro", prompt);
        Assert.Contains("Similarity score: 0.910000", prompt);
        Assert.Contains("USER QUESTION", prompt);
        Assert.Contains("Que receitas tens com frango?", prompt);
    }

    [Fact]
    public void PromptBuilder_RendersLocalizationRules_ForEnglishWithFallbackMode()
    {
        var builder = new PromptBuilder();
        var recipe = new RetrievalRecipe("1", "1", "Salsa Verde Chicken", "Dinner", "quick", ["chicken", "cilantro"], "Slice", "45", 0.91, "canonical-multilingual-projection");

        var prompt = builder.Build(
            "What recipes do you have with chicken?",
            [recipe],
            "RecipeDiscovery",
            LocalizationOptions.Create("en", ["es"], strictMode: false),
            requestedLanguage: "en");

        Assert.Contains("Requested language: en", prompt);
        Assert.Contains("Resolved language: en", prompt);
        Assert.Contains("Strict mode: False", prompt);
        Assert.Contains("Fallback language: es", prompt);
        Assert.DoesNotContain("Strict mode is enabled: never answer using another language.", prompt);
    }

    [Theory]
    [InlineData("RecipeDiscovery", "List every matching recipe found in context. Do not focus on only one recipe.")]
    [InlineData("RecipeDetails", "Answer only about the requested recipe. Do not expand to other recipes unless explicitly requested.")]
    [InlineData("IngredientSearch", "Explain which retrieved recipes contain the requested ingredient and cite recipe titles.")]
    [InlineData("MealPlanning", "Generate a meal plan using only the retrieved recipes.")]
    [InlineData("GeneralConversation", "Answer normally while still grounded in the provided repository context.")]
    public void PromptBuilder_RendersIntentSpecificInstructions(string intentType, string expectedInstruction)
    {
        var builder = new PromptBuilder();
        var recipe = new RetrievalRecipe("1", "1", "Spicy Chicken", "Dinner", "spicy", ["chicken"], "Slice", "45", 0.91, "canonical-multilingual-projection");

        var prompt = builder.Build("What can I cook?", [recipe], intentType, LocalizationOptions.Create("en"), requestedLanguage: "en");

        Assert.Contains($"- Type: {intentType}", prompt);
        Assert.Contains(expectedInstruction, prompt);
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
                "1#pt" => new RecipeMetadata("1", "Frango Picante", "frango picante", "Jantar de frango", "picante", ["frango"], "Cozinhar", "45"),
                "2" => new RecipeMetadata("2", "Beef Stir Fry", "beef stir fry", "Beef dinner", "beef", ["beef"], "Stir fry", "30"),
                "3" => new RecipeMetadata("3", "Garlic Rice", "garlic rice", "Rice side", "rice", ["rice"], "Boil", "20"),
                _ => new RecipeMetadata(recipeId, $"Recipe {recipeId}", string.Empty)
            };

            return Task.FromResult(metadata);
        }
    }

    private sealed class StrictAwareLocalizedMetadataProvider : ISemanticRecipeMetadataProvider, ILocalizedSemanticRecipeMetadataProvider
    {
        public Task<RecipeMetadata> GetMetadataAsync(string recipeId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RecipeMetadata(recipeId, "Spicy Chicken", "spicy chicken", "Dinner", "spicy", ["chicken"], "Cook", "45"));

        public Task<RecipeMetadata?> GetMetadataAsync(
            string recipeId,
            LocalizationOptions localizationOptions,
            CancellationToken cancellationToken = default)
        {
            if (localizationOptions.StrictMode && localizationOptions.PreferredLanguage.Equals("pt", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<RecipeMetadata?>(null);
            }

            return Task.FromResult<RecipeMetadata?>(new RecipeMetadata(recipeId, "Spicy Chicken", "spicy chicken", "Dinner", "spicy", ["chicken"], "Cook", "45"));
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
