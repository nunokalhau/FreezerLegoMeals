using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using VectorStores.DotNet;
using Xunit;

namespace WebApi.DotNet.IntegrationTests;

[Collection("IntegrationTests")]
public class ChromaVectorStoreIntegrationTests
{
    private const string ChromaBaseUrl = "http://localhost:8001";
    private const string Tenant = "default_tenant";
    private const string Database = "default_database";

    [ChromaAvailableFact]
    public async Task ChromaVectorStore_CreatesCollectionAndReusesItAcrossInstances()
    {
        var collectionName = $"itest_recipe_embeddings_{Guid.NewGuid():N}";
        var options = Options.Create(new ChromaVectorStoreOptions
        {
            BaseUrl = ChromaBaseUrl,
            Tenant = Tenant,
            Database = Database,
            CollectionName = collectionName,
            Timeout = TimeSpan.FromSeconds(15)
        });

        using var chromaClient = CreateHttpClient();
        string? collectionId = null;

        try
        {
            var firstStore = new ChromaVectorStore(CreateHttpClient(), options);

            var initiallyEmpty = await firstStore.SearchAsync([1f, 0f], 2);
            Assert.Empty(initiallyEmpty);

            collectionId = await GetCollectionIdByNameAsync(chromaClient, collectionName);
            Assert.False(string.IsNullOrWhiteSpace(collectionId));

            await UpsertEmbeddingsAsync(
                chromaClient,
                collectionId!,
                ["recipe-1", "recipe-2"],
                [[1f, 0f], [0f, 1f]]);

            var firstResults = await firstStore.SearchAsync([1f, 0f], 2);
            Assert.Equal(new[] { "recipe-1", "recipe-2" }, firstResults.Select(match => match.RecipeId).ToArray());
            Assert.True(firstResults[0].Score >= firstResults[1].Score);

            var secondStore = new ChromaVectorStore(CreateHttpClient(), options);
            var secondResults = await secondStore.SearchAsync([1f, 0f], 2);
            Assert.Equal(new[] { "recipe-1", "recipe-2" }, secondResults.Select(match => match.RecipeId).ToArray());
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(collectionId))
            {
                await DeleteCollectionAsync(chromaClient, collectionId);
            }
        }
    }

    internal static async Task<ChromaAvailability> GetChromaAvailabilityAsync()
    {
        using var client = CreateHttpClient();

        try
        {
            using var response = await client.GetAsync("/api/v2/heartbeat");
            if (!response.IsSuccessStatusCode)
            {
                return ChromaAvailability.Unavailable($"Local ChromaDB is required at {ChromaBaseUrl}; /api/v2/heartbeat returned {(int)response.StatusCode}.");
            }

            return ChromaAvailability.Available();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return ChromaAvailability.Unavailable($"Local ChromaDB is required at {ChromaBaseUrl}; heartbeat failed: {ex.Message}");
        }
    }

    internal sealed record ChromaAvailability(bool IsAvailable, string? SkipReason)
    {
        public static ChromaAvailability Available() => new(true, null);

        public static ChromaAvailability Unavailable(string skipReason) => new(false, skipReason);
    }

    private static HttpClient CreateHttpClient()
    {
        return new HttpClient
        {
            BaseAddress = new Uri(ChromaBaseUrl),
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    private static async Task<string?> GetCollectionIdByNameAsync(HttpClient client, string collectionName)
    {
        using var response = await client.GetAsync($"/api/v2/tenants/{Uri.EscapeDataString(Tenant)}/databases/{Uri.EscapeDataString(Database)}/collections");
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (!item.TryGetProperty("name", out var nameProperty) || !item.TryGetProperty("id", out var idProperty))
                continue;

            var name = nameProperty.GetString();
            if (string.Equals(name, collectionName, StringComparison.Ordinal))
            {
                return idProperty.GetString();
            }
        }

        return null;
    }

    private static async Task UpsertEmbeddingsAsync(HttpClient client, string collectionId, IReadOnlyList<string> ids, IReadOnlyList<IReadOnlyList<float>> embeddings)
    {
        var payload = new
        {
            ids,
            embeddings
        };

        using var response = await client.PostAsJsonAsync(
            $"/api/v2/tenants/{Uri.EscapeDataString(Tenant)}/databases/{Uri.EscapeDataString(Database)}/collections/{Uri.EscapeDataString(collectionId)}/upsert",
            payload);
        response.EnsureSuccessStatusCode();
    }

    private static async Task DeleteCollectionAsync(HttpClient client, string collectionId)
    {
        using var deleteByIdRequest = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/v2/tenants/{Uri.EscapeDataString(Tenant)}/databases/{Uri.EscapeDataString(Database)}/collections/by-id/{Uri.EscapeDataString(collectionId)}");
        using var deleteByIdResponse = await client.SendAsync(deleteByIdRequest);
        if (deleteByIdResponse.IsSuccessStatusCode)
            return;

        // Chroma API variants may expose collection deletion under a POST /delete endpoint.
        using var deleteRequest = await client.PostAsJsonAsync(
            $"/api/v2/tenants/{Uri.EscapeDataString(Tenant)}/databases/{Uri.EscapeDataString(Database)}/collections/{Uri.EscapeDataString(collectionId)}/delete",
            new { ids = Array.Empty<string>() });
        _ = deleteRequest.IsSuccessStatusCode;
    }
}

public sealed class ChromaAvailableFactAttribute : FactAttribute
{
    public ChromaAvailableFactAttribute()
    {
        var availability = ChromaVectorStoreIntegrationTests.GetChromaAvailabilityAsync().GetAwaiter().GetResult();
        if (!availability.IsAvailable)
        {
            Skip = availability.SkipReason;
        }
    }
}
