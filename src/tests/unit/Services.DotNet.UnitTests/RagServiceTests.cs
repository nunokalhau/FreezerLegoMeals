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
        Assert.Equal(0.91, Assert.Single(result.Sources).SimilarityScore);
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

    private sealed class StubVectorStore : IVectorStore
    {
        private readonly double _score;

        public StubVectorStore(double score)
        {
            _score = score;
        }

        public Task<IReadOnlyList<VectorMatch>> SearchAsync(IReadOnlyList<float> queryEmbedding, int topK, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<VectorMatch>>([new VectorMatch("1", _score)]);
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
}
