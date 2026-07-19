using Embedding.DotNet;
using Microsoft.AspNetCore.Mvc;
using SemanticSearch.DotNet;
using VectorStores.DotNet;
using WebApi.DotNet.Contracts.Requests;
using WebApi.DotNet.Controllers;
using Xunit;

namespace WebApi.DotNet.UnitTests;

public class SemanticSearchControllerTests
{
    [Fact]
    public async Task Search_WithValidRequestReturnsResults()
    {
        var controller = new SemanticSearchController(new SemanticSearchService(
            new StubEmbeddingService(),
            new StubVectorStore(),
            new StubMetadataProvider()));

        var result = await controller.Search(new SemanticSearchRequest { Query = "spicy", TopK = 1 }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsAssignableFrom<IReadOnlyList<WebApi.DotNet.Contracts.Responses.SemanticSearchResponse>>(ok.Value);
        Assert.Single(payload);
        Assert.Equal("Spicy Chicken", payload[0].Title);
    }

    [Fact]
    public async Task Search_WithBlankQueryReturnsBadRequest()
    {
        var controller = new SemanticSearchController(new SemanticSearchService(new StubEmbeddingService(), new StubVectorStore(), new StubMetadataProvider()));

        var result = await controller.Search(new SemanticSearchRequest { Query = " " }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    private sealed class StubEmbeddingService : IEmbeddingService
    {
        public Task<EmbeddingResponse> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default) =>
            Task.FromResult(new EmbeddingResponse("test", 2, new[] { 1f, 0f }));
    }

    private sealed class StubVectorStore : IVectorStore
    {
        public Task<IReadOnlyList<VectorMatch>> SearchAsync(IReadOnlyList<float> queryEmbedding, int topK, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<VectorMatch>>([new VectorMatch("1", 1)]);
    }

    private sealed class StubMetadataProvider : ISemanticRecipeMetadataProvider
    {
        public Task<RecipeMetadata> GetMetadataAsync(string recipeId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RecipeMetadata(recipeId, "Spicy Chicken", "spicy chicken dinner"));
    }
}