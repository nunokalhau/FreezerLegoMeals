using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WebApi.DotNet.Contracts.Requests;
using WebApi.DotNet.Contracts.Responses;
using Xunit;

namespace WebApi.DotNet.IntegrationTests;

[Collection("IntegrationTests")]
public class EmbeddingsControllerIntegrationTests
{
    private const string OllamaBaseUrl = "http://localhost:11434";
    private const string EmbeddingModel = "nomic-embed-text";

    [OllamaEmbeddingAvailableFact]
    public async Task GenerateEmbedding_WithLocalOllama_ReturnsVector()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Embeddings:OllamaBaseUrl"] = OllamaBaseUrl,
                    ["Embeddings:Model"] = EmbeddingModel,
                    ["Embeddings:Timeout"] = "00:01:00"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<FreezerLegoMealsContext>>();
                services.RemoveAll<FreezerLegoMealsContext>();
                services.AddDbContext<FreezerLegoMealsContext>(options =>
                    options.UseInMemoryDatabase("EmbeddingIntegrationTestDatabase"));
            });
        });

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/embeddings", new EmbeddingRequest { Text = "Chicken curry with rice" });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.NotNull(payload);
        Assert.Equal(EmbeddingModel, payload.Model);
        Assert.True(payload.Dimensions > 0);
        Assert.Equal(payload.Dimensions, payload.Embedding.Count);
    }

    internal static async Task<OllamaEmbeddingAvailability> GetOllamaEmbeddingAvailabilityAsync()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri(OllamaBaseUrl), Timeout = TimeSpan.FromSeconds(5) };

        try
        {
            var tags = await httpClient.GetFromJsonAsync<OllamaTagsResponse>("/api/tags");
            var names = tags?.Models?.Select(model => model.Name.Split(':')[0]).ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
            return names.Contains(EmbeddingModel)
                ? OllamaEmbeddingAvailability.Available()
                : OllamaEmbeddingAvailability.Unavailable($"Ollama model {EmbeddingModel} is not installed.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return OllamaEmbeddingAvailability.Unavailable($"Local Ollama is unavailable: {ex.Message}");
        }
    }

    internal sealed record OllamaEmbeddingAvailability(bool IsAvailable, string? SkipReason)
    {
        public static OllamaEmbeddingAvailability Available() => new(true, null);

        public static OllamaEmbeddingAvailability Unavailable(string skipReason) => new(false, skipReason);
    }

    private sealed record OllamaTagsResponse(IEnumerable<OllamaModel>? Models);

    private sealed record OllamaModel(string Name);
}

public sealed class OllamaEmbeddingAvailableFactAttribute : FactAttribute
{
    public OllamaEmbeddingAvailableFactAttribute()
    {
        var availability = EmbeddingsControllerIntegrationTests.GetOllamaEmbeddingAvailabilityAsync().GetAwaiter().GetResult();
        if (!availability.IsAvailable)
        {
            Skip = availability.SkipReason;
        }
    }
}