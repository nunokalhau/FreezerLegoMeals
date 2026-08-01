using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RAG.DotNet;
using VectorStores.DotNet;
using WebApi.DotNet.Contracts.Requests;
using WebApi.DotNet.Contracts.Responses;
using Xunit;

namespace WebApi.DotNet.IntegrationTests;

[Collection("IntegrationTests")]
public class AdminControllerIntegrationTests
{
    [Fact]
    public async Task Reindex_EnsuresClearsAndIndexesAndReturnsStatistics()
    {
        var operations = new List<string>();
        var vectorStore = new RecordingVectorStore(operations);
        var indexingService = new RecordingRecipeIndexingService(
            operations,
            new RecipeIndexingResult(
                TotalRecipes: 3,
                IndexedRecipes: 2,
                FailedRecipes: 1,
                EmbeddingModel: "nomic-embed-text",
                EmbeddingDimensions: 768,
                DurationMs: 42));

        using var factory = CreateFactory(vectorStore, indexingService, "admin_test_collection");
        using var client = factory.CreateClient();

        var operationsBeforeCall = operations.Count;
        var ensureCallsBeforeCall = vectorStore.EnsureCalls;
        var clearCallsBeforeCall = vectorStore.ClearCalls;
        var indexCallsBeforeCall = indexingService.CallCount;

        var response = await client.PostAsJsonAsync("/api/admin/reindex", new { });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<AdminReindexResponse>();

        Assert.NotNull(payload);
        Assert.Equal(2, payload.RecipesIndexed);
        Assert.Equal(3, payload.TotalRecipes);
        Assert.Equal(1, payload.Failures);
        Assert.Equal("nomic-embed-text", payload.EmbeddingModel);
        Assert.Equal("admin_test_collection", payload.CollectionName);
        Assert.True(payload.ElapsedMs >= 0);

        var reindexOperations = operations.Skip(operationsBeforeCall).ToArray();
        Assert.Equal(new[] { "ensure", "clear", "index" }, reindexOperations);
        Assert.Equal(ensureCallsBeforeCall + 1, vectorStore.EnsureCalls);
        Assert.Equal(clearCallsBeforeCall + 1, vectorStore.ClearCalls);
        Assert.Equal(indexCallsBeforeCall + 1, indexingService.CallCount);
    }

    [Fact]
    public async Task Reindex_EndpointIsSeparateFromAssistantEndpoint()
    {
        var operations = new List<string>();
        var vectorStore = new RecordingVectorStore(operations);
        var indexingService = new RecordingRecipeIndexingService(
            operations,
            new RecipeIndexingResult(0, 0, 0, string.Empty, 0, 0));

        using var factory = CreateFactory(vectorStore, indexingService, "recipe_embeddings");
        using var client = factory.CreateClient();

        var indexCallsBeforeAssistant = indexingService.CallCount;

        var assistantResponse = await client.PostAsJsonAsync("/api/assistant/chat", new AssistantChatRequest
        {
            Message = " "
        });

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, assistantResponse.StatusCode);
        Assert.Equal(indexCallsBeforeAssistant, indexingService.CallCount);

        var indexCallsBeforeAdmin = indexingService.CallCount;
        var adminResponse = await client.PostAsJsonAsync("/api/admin/reindex", new { });
        adminResponse.EnsureSuccessStatusCode();
        Assert.Equal(indexCallsBeforeAdmin + 1, indexingService.CallCount);
    }

    private static WebApplicationFactory<Program> CreateFactory(
        RecordingVectorStore vectorStore,
        RecordingRecipeIndexingService indexingService,
        string collectionName)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ChromaVectorStore:CollectionName"] = collectionName
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<FreezerLegoMealsContext>>();
                services.RemoveAll<FreezerLegoMealsContext>();
                services.AddDbContext<FreezerLegoMealsContext>(options =>
                    options.UseInMemoryDatabase($"AdminReindexIntegrationTestDb-{Guid.NewGuid():N}"));

                services.RemoveAll<IVectorStore>();
                services.RemoveAll<IRecipeIndexingService>();
                services.AddSingleton<IVectorStore>(vectorStore);
                services.AddSingleton<IRecipeIndexingService>(indexingService);
            });
        });
    }

    private sealed class RecordingVectorStore : IVectorStore
    {
        private readonly IList<string> _operations;

        public RecordingVectorStore(IList<string> operations)
        {
            _operations = operations;
        }

        public int EnsureCalls { get; private set; }

        public int ClearCalls { get; private set; }

        public Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default)
        {
            EnsureCalls++;
            _operations.Add("ensure");
            return Task.CompletedTask;
        }

        public Task UpsertAsync(IReadOnlyList<VectorDocument> documents, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteAsync(IReadOnlyList<string> recipeIds, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ClearCollectionAsync(CancellationToken cancellationToken = default)
        {
            ClearCalls++;
            _operations.Add("clear");
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<VectorMatch>> SearchAsync(IReadOnlyList<float> queryEmbedding, int topK, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<VectorMatch>>([]);
        }
    }

    private sealed class RecordingRecipeIndexingService : IRecipeIndexingService
    {
        private readonly IList<string> _operations;
        private readonly RecipeIndexingResult _result;

        public RecordingRecipeIndexingService(IList<string> operations, RecipeIndexingResult result)
        {
            _operations = operations;
            _result = result;
        }

        public int CallCount { get; private set; }

        public Task<RecipeIndexingResult> IndexAllRecipesAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;
            _operations.Add("index");
            return Task.FromResult(_result);
        }
    }
}
