using System.Net.Http.Json;
using Embedding.DotNet;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SemanticSearch.DotNet;
using VectorStores.DotNet;
using WebApi.DotNet.Contracts.Requests;
using WebApi.DotNet.Contracts.Responses;
using Xunit;

namespace WebApi.DotNet.IntegrationTests;

[Collection("IntegrationTests")]
public class SemanticSearchControllerIntegrationTests
{
    [Fact]
    public async Task Search_ReturnsSemanticResults()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IEmbeddingService>();
                services.RemoveAll<IVectorStore>();
                services.RemoveAll<ISemanticRecipeMetadataProvider>();
                services.AddSingleton<IEmbeddingService, StubEmbeddingService>();
                services.AddSingleton<IVectorStore, StubVectorStore>();
                services.AddSingleton<ISemanticRecipeMetadataProvider, StubMetadataProvider>();
            });
        });

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/semantic-search", new SemanticSearchRequest { Query = "spicy", TopK = 1 });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<List<SemanticSearchResponse>>();

        Assert.NotNull(payload);
        var result = Assert.Single(payload);
        Assert.Equal("1", result.RecipeId);
        Assert.Equal("Spicy Chicken", result.Title);
    }

    private sealed class StubEmbeddingService : IEmbeddingService
    {
        public Task<Embedding.DotNet.EmbeddingResponse> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Embedding.DotNet.EmbeddingResponse("test", 2, new[] { 1f, 0f }));
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