using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RAG.DotNet;
using Services.DotNet;
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

    [Fact]
    public async Task Chat_WithRepositoryQuestion_UsesRagAndKeepsPublicContract()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<FreezerLegoMealsContext>>();
                services.RemoveAll<FreezerLegoMealsContext>();
                services.AddDbContext<FreezerLegoMealsContext>(options =>
                    options.UseInMemoryDatabase("AssistantRagIntegrationTestDatabase"));
                services.RemoveAll<IOllamaClient>();
                services.RemoveAll<IRetrievalService>();
                services.RemoveAll<IPromptBuilder>();
                services.AddSingleton<IOllamaClient, StubOllamaClient>();
                services.AddSingleton<IRetrievalService, StubRetrievalService>();
                services.AddSingleton<IPromptBuilder, StubPromptBuilder>();
            });
        });

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/assistant/chat", new AssistantChatRequest
        {
            Message = "What spicy chicken meal can I cook?"
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<AssistantChatResponse>(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload.ConversationId));
        Assert.Contains("Use the spicy chicken recipe.", payload.Response);
        Assert.Contains("Sources:", payload.Response);
        Assert.Contains("1: Spicy Chicken", payload.Response);
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

    private sealed class StubOllamaClient : IOllamaClient
    {
        private int _calls;

        public Task<OllamaChatResult> ChatAsync(string? model, IReadOnlyList<ConversationMessage> messages, IReadOnlyList<ToolDefinition> tools, CancellationToken cancellationToken = default)
        {
            _calls++;
            return Task.FromResult(_calls == 1
                ? new OllamaChatResult("direct draft", [])
                : new OllamaChatResult("Use the spicy chicken recipe.", []));
        }
    }

    private sealed class StubRetrievalService : IRetrievalService
    {
        public Task<RetrievalResult> RetrieveAsync(string question, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RetrievalResult(
                question,
                [new RetrievalRecipe("1", "Spicy Chicken", "Dinner", "spicy", ["chicken"], "Slice", "45", 0.91)],
                [new SourceAttribution("1", "Spicy Chicken", 0.91)]));
    }

    private sealed class StubPromptBuilder : IPromptBuilder
    {
        public string Build(string question, IReadOnlyList<RetrievalRecipe> recipes) => "rag prompt";
    }
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