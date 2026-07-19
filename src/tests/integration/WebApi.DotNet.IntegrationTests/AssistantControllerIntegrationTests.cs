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
public class AssistantControllerIntegrationTests
{
    private const string OllamaBaseUrl = "http://localhost:11434";
    private const string OllamaRequiredMessage = "Local Ollama instance is required at http://localhost:11434 with at least one available model.";

    [OllamaAvailableFact]
    public async Task Chat_WithLocalOllama_ReturnsConversationIdAndMaintainsConversation()
    {
        var availability = await GetOllamaAvailabilityAsync();
        if (!availability.IsAvailable)
        {
            return;
        }

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Ollama:BaseUrl"] = OllamaBaseUrl,
                    ["Ollama:DefaultModel"] = availability.Model,
                    ["Ollama:Timeout"] = "00:01:00"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<FreezerLegoMealsContext>>();
                services.RemoveAll<FreezerLegoMealsContext>();
                services.AddDbContext<FreezerLegoMealsContext>(options =>
                    options.UseInMemoryDatabase("AssistantIntegrationTestDatabase"));
            });
        });

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/assistant/chat", new AssistantChatRequest
        {
            Message = "Reply with the single word: OK"
        });

        response.EnsureSuccessStatusCode();
        Assert.Contains("application/json", response.Content.Headers.ContentType?.ToString());

        var jsonResponse = await response.Content.ReadAsStringAsync();
        var responseObject = JsonSerializer.Deserialize<AssistantChatResponse>(jsonResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.NotNull(responseObject);
        Assert.False(string.IsNullOrWhiteSpace(responseObject.ConversationId));
        Assert.False(string.IsNullOrWhiteSpace(responseObject.Response));

        var followUpResponse = await client.PostAsJsonAsync("/api/assistant/chat", new AssistantChatRequest
        {
            ConversationId = responseObject.ConversationId,
            Message = "Reply with the single word: OK"
        });

        followUpResponse.EnsureSuccessStatusCode();
        var followUpObject = await followUpResponse.Content.ReadFromJsonAsync<AssistantChatResponse>(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.NotNull(followUpObject);
        Assert.Equal(responseObject.ConversationId, followUpObject.ConversationId);
        Assert.False(string.IsNullOrWhiteSpace(followUpObject.Response));
    }

    internal static async Task<OllamaAvailability> GetOllamaAvailabilityAsync()
    {
        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri(OllamaBaseUrl),
            Timeout = TimeSpan.FromSeconds(5)
        };

        try
        {
            using var response = await httpClient.GetAsync("/api/tags");
            if (!response.IsSuccessStatusCode)
                return OllamaAvailability.Unavailable($"{OllamaRequiredMessage} GET /api/tags returned {(int)response.StatusCode}.");

            var tags = await response.Content.ReadFromJsonAsync<OllamaTagsResponse>();
            var model = tags?.Models?.FirstOrDefault(model =>
                model.Capabilities?.Contains("completion", StringComparer.OrdinalIgnoreCase) == true)?.Name;
            if (string.IsNullOrWhiteSpace(model))
                return OllamaAvailability.Unavailable($"{OllamaRequiredMessage} GET /api/tags returned no completion-capable models.");

            return OllamaAvailability.Available(model);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return OllamaAvailability.Unavailable($"{OllamaRequiredMessage} GET /api/tags failed or timed out: {ex.Message}");
        }
    }

    internal sealed record OllamaAvailability(bool IsAvailable, string? Model, string? SkipReason)
    {
        public static OllamaAvailability Available(string model) => new(true, model, null);

        public static OllamaAvailability Unavailable(string skipReason) => new(false, null, skipReason);
    }

    private sealed record OllamaTagsResponse(IEnumerable<OllamaModel>? Models);

    private sealed record OllamaModel(string Name, IEnumerable<string>? Capabilities);
}

public sealed class OllamaAvailableFactAttribute : FactAttribute
{
    public OllamaAvailableFactAttribute()
    {
        var availability = AssistantControllerIntegrationTests.GetOllamaAvailabilityAsync().GetAwaiter().GetResult();
        if (!availability.IsAvailable)
        {
            Skip = availability.SkipReason;
        }
    }
}