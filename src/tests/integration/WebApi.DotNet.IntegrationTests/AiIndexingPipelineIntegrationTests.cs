using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
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
public sealed class AiIndexingPipelineIntegrationTests
{
    private const string ChromaBaseUrl = "http://localhost:8001";
    private const string ChromaTenant = "default_tenant";
    private const string ChromaDatabase = "default_database";

    [OllamaAndChromaAvailableFact]
    public async Task AssistantQuery_AfterAdminReindex_UsesIndexedRecipesAndReturnsSources()
    {
        var ollamaAvailability = await AssistantControllerIntegrationTests.GetOllamaAvailabilityAsync();
        var collectionName = $"itest_recipe_pipeline_{Guid.NewGuid():N}";
        await using var sqliteConnection = new SqliteConnection("Data Source=:memory:");
        await sqliteConnection.OpenAsync();

        using var factory = CreateFactory(sqliteConnection, collectionName, ollamaAvailability.Model!);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<FreezerLegoMealsContext>();
            await context.Database.EnsureCreatedAsync();
            IntegrationTestDbSeeder.SeedTestData(context);
        }

        using var client = factory.CreateClient();

        try
        {
            using (var scope = factory.Services.CreateScope())
            {
                var vectorStore = scope.ServiceProvider.GetRequiredService<IVectorStore>();
                var indexingService = scope.ServiceProvider.GetRequiredService<IRecipeIndexingService>();

                await vectorStore.EnsureCollectionExistsAsync();
                var indexingResult = await indexingService.IndexAllRecipesAsync();
                Assert.Equal(4, indexingResult.TotalRecipes);
                Assert.Equal(4, indexingResult.IndexedRecipes);
                Assert.Equal(0, indexingResult.FailedRecipes);
            }

            var assistantResponse = await client.PostAsJsonAsync("/api/assistant/chat", new AssistantChatRequest
            {
                Message = "What chicken and rice freezer meal recipe should I prep this week?"
            });
            assistantResponse.EnsureSuccessStatusCode();

            var assistantPayload = await assistantResponse.Content.ReadFromJsonAsync<AssistantChatResponse>(new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            Assert.NotNull(assistantPayload);
            Assert.False(string.IsNullOrWhiteSpace(assistantPayload.ConversationId));
            Assert.Contains("Sources:", assistantPayload.Response, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Sources: none", assistantPayload.Response, StringComparison.OrdinalIgnoreCase);

            var containsSeededRecipe =
                assistantPayload.Response.Contains("Chicken Fried Rice", StringComparison.OrdinalIgnoreCase) ||
                assistantPayload.Response.Contains("Beef Stir Fry", StringComparison.OrdinalIgnoreCase) ||
                assistantPayload.Response.Contains("Broccoli Beef", StringComparison.OrdinalIgnoreCase) ||
                assistantPayload.Response.Contains("Tomato Garlic Pasta", StringComparison.OrdinalIgnoreCase);
            Assert.True(containsSeededRecipe, "Assistant response should include source attribution for a seeded recipe.");
        }
        finally
        {
            await DeleteCollectionIfExistsAsync(collectionName);
        }
    }

    [OllamaAndChromaAvailableFact]
    public async Task AssistantQuery_UnrelatedQuestion_DoesNotRetrieveRecipes()
    {
        var ollamaAvailability = await AssistantControllerIntegrationTests.GetOllamaAvailabilityAsync();
        var collectionName = $"itest_recipe_pipeline_{Guid.NewGuid():N}";
        await using var sqliteConnection = new SqliteConnection("Data Source=:memory:");
        await sqliteConnection.OpenAsync();

        using var factory = CreateFactory(sqliteConnection, collectionName, ollamaAvailability.Model!);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<FreezerLegoMealsContext>();
            await context.Database.EnsureCreatedAsync();
            IntegrationTestDbSeeder.SeedTestData(context);
        }

        using var client = factory.CreateClient();

        try
        {
            using (var scope = factory.Services.CreateScope())
            {
                var vectorStore = scope.ServiceProvider.GetRequiredService<IVectorStore>();
                var indexingService = scope.ServiceProvider.GetRequiredService<IRecipeIndexingService>();

                await vectorStore.EnsureCollectionExistsAsync();
                var indexingResult = await indexingService.IndexAllRecipesAsync();
                Assert.Equal(4, indexingResult.TotalRecipes);
                Assert.Equal(4, indexingResult.IndexedRecipes);
                Assert.Equal(0, indexingResult.FailedRecipes);
            }

            var assistantResponse = await client.PostAsJsonAsync("/api/assistant/chat", new AssistantChatRequest
            {
                Message = "What is the speed of light in vacuum?"
            });
            assistantResponse.EnsureSuccessStatusCode();

            var assistantPayload = await assistantResponse.Content.ReadFromJsonAsync<AssistantChatResponse>(new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            Assert.NotNull(assistantPayload);
            Assert.DoesNotContain("Chicken Fried Rice", assistantPayload.Response, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Beef Stir Fry", assistantPayload.Response, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Broccoli Beef", assistantPayload.Response, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Tomato Garlic Pasta", assistantPayload.Response, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Sources:\n-", assistantPayload.Response, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await DeleteCollectionIfExistsAsync(collectionName);
        }
    }

    private static WebApplicationFactory<Program> CreateFactory(SqliteConnection sqliteConnection, string collectionName, string ollamaModel)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Ollama:BaseUrl"] = "http://localhost:11434",
                    ["Ollama:DefaultModel"] = ollamaModel,
                    ["Ollama:Timeout"] = "00:02:00",
                    ["Embeddings:OllamaBaseUrl"] = "http://localhost:11434",
                    ["Embeddings:Model"] = "nomic-embed-text",
                    ["Embeddings:Timeout"] = "00:01:00",
                    ["ChromaVectorStore:BaseUrl"] = ChromaBaseUrl,
                    ["ChromaVectorStore:Tenant"] = ChromaTenant,
                    ["ChromaVectorStore:Database"] = ChromaDatabase,
                    ["ChromaVectorStore:CollectionName"] = collectionName,
                    ["ChromaVectorStore:Timeout"] = "00:00:30",
                    ["AI:Indexing:Enabled"] = "false"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<FreezerLegoMealsContext>>();
                services.RemoveAll<FreezerLegoMealsContext>();
                services.AddDbContext<FreezerLegoMealsContext>(options => options.UseSqlite(sqliteConnection));
            });
        });
    }

    private static async Task DeleteCollectionIfExistsAsync(string collectionName)
    {
        using var client = new HttpClient
        {
            BaseAddress = new Uri(ChromaBaseUrl),
            Timeout = TimeSpan.FromSeconds(10)
        };

        using var listResponse = await client.GetAsync($"/api/v2/tenants/{Uri.EscapeDataString(ChromaTenant)}/databases/{Uri.EscapeDataString(ChromaDatabase)}/collections");
        if (!listResponse.IsSuccessStatusCode)
            return;

        await using var stream = await listResponse.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        string? collectionId = null;
        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (!item.TryGetProperty("name", out var nameProperty) || !item.TryGetProperty("id", out var idProperty))
                continue;

            if (string.Equals(nameProperty.GetString(), collectionName, StringComparison.Ordinal))
            {
                collectionId = idProperty.GetString();
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(collectionId))
            return;

        using var deleteRequest = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/v2/tenants/{Uri.EscapeDataString(ChromaTenant)}/databases/{Uri.EscapeDataString(ChromaDatabase)}/collections/by-id/{Uri.EscapeDataString(collectionId)}");
        using var deleteResponse = await client.SendAsync(deleteRequest);
        _ = deleteResponse.IsSuccessStatusCode;
    }
}

public sealed class OllamaAndChromaAvailableFactAttribute : FactAttribute
{
    public OllamaAndChromaAvailableFactAttribute()
    {
        var ollamaAvailability = AssistantControllerIntegrationTests.GetOllamaAvailabilityAsync().GetAwaiter().GetResult();
        if (!ollamaAvailability.IsAvailable)
        {
            Skip = ollamaAvailability.SkipReason;
            return;
        }

        var chromaAvailability = ChromaVectorStoreIntegrationTests.GetChromaAvailabilityAsync().GetAwaiter().GetResult();
        if (!chromaAvailability.IsAvailable)
        {
            Skip = chromaAvailability.SkipReason;
        }
    }
}