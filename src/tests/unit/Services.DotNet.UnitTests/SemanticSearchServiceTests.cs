using Embedding.DotNet;
using SemanticSearch.DotNet;
using VectorStores.DotNet;
using Xunit;

namespace Services.DotNet.UnitTests;

public class SemanticSearchServiceTests
{
    [Fact]
    public void CosineSimilarity_CalculatesExpectedValues()
    {
        Assert.Equal(1, CosineSimilarity.Calculate([1, 0], [1, 0]));
        Assert.Equal(0, CosineSimilarity.Calculate([1, 0], [0, 1]));
        Assert.Equal(0, CosineSimilarity.Calculate([1, 0], []));
    }

    [Fact]
    public async Task LocalVectorStore_RanksTopKAndCachesEmbeddings()
    {
        var directory = CreateTempDirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "1.json"), "{\"recipeId\":\"1\",\"embedding\":[1,0]}");
            await File.WriteAllTextAsync(Path.Combine(directory, "2.json"), "{\"recipeId\":\"2\",\"embedding\":[0,1]}");
            var store = new LocalVectorStore(directory);

            var matches = await store.SearchAsync([1, 0], 1);
            File.Delete(Path.Combine(directory, "1.json"));
            var cachedMatches = await store.SearchAsync([1, 0], 2);

            Assert.Equal("1", matches.Single().RecipeId);
            Assert.Equal(new[] { "1", "2" }, cachedMatches.Select(match => match.RecipeId));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task LocalVectorStore_ReturnsEmptyForEmptyIndex()
    {
        var directory = CreateTempDirectory();
        try
        {
            Assert.Empty(await new LocalVectorStore(directory).SearchAsync([1, 0], 5));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task SemanticSearchService_ReturnsRichRankedResults()
    {
        var service = new SemanticSearchService(
            new StubEmbeddingService(),
            new StubVectorStore(),
            new StubMetadataProvider());

        var results = await service.SearchAsync("spicy dinner", 1);

        var result = Assert.Single(results);
        Assert.Equal("1", result.RecipeId);
        Assert.Equal("Spicy Chicken", result.Title);
        Assert.Equal(1, result.Score);
        Assert.Contains("chicken", result.MatchedText);
        Assert.Contains("High semantic similarity", result.Reason);
    }

    [Fact]
    public async Task SemanticSearchService_ReturnsEmptyForBlankQueryOrInvalidTopK()
    {
        var service = new SemanticSearchService(new StubEmbeddingService(), new StubVectorStore(), new StubMetadataProvider());

        Assert.Empty(await service.SearchAsync(" ", 5));
        Assert.Empty(await service.SearchAsync("anything", 0));
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class StubEmbeddingService : IEmbeddingService
    {
        public Task<EmbeddingResponse> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default) =>
            Task.FromResult(new EmbeddingResponse("test", 2, new[] { 1f, 0f }));
    }

    private sealed class StubVectorStore : IVectorStore
    {
        public Task<IReadOnlyList<VectorMatch>> SearchAsync(IReadOnlyList<float> queryEmbedding, int topK, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<VectorMatch>>(new[] { new VectorMatch("1", 1) }.Take(topK).ToList());
    }

    private sealed class StubMetadataProvider : ISemanticRecipeMetadataProvider
    {
        public Task<RecipeMetadata> GetMetadataAsync(string recipeId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RecipeMetadata(recipeId, "Spicy Chicken", "spicy chicken dinner"));
    }
}