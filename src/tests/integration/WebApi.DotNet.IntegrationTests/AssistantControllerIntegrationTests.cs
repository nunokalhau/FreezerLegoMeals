using System.Net.Http.Json;
using System.Text.Json;
using AI.Memory.DotNet;
using Domain.DotNet;
using Embedding.DotNet;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orchestration.DotNet;
using RAG.DotNet;
using SemanticSearch.DotNet;
using Services.DotNet;
using StackExchange.Redis;
using VectorStores.DotNet;
using WebApi.DotNet.Contracts.Requests;
using WebApi.DotNet.Contracts.Responses;
using Xunit;

namespace WebApi.DotNet.IntegrationTests;

[Collection("IntegrationTests")]
public class AssistantControllerIntegrationTests
{
    private const string OllamaBaseUrl = "http://localhost:11434";
    private const string OllamaRequiredMessage = "Local Ollama instance is required at http://localhost:11434 with at least one available model.";
    private const string RedisConnectionString = "localhost:6379,abortConnect=false,connectTimeout=2000,syncTimeout=2000";
    private const string RedisRequiredMessage = "Local Redis is required at localhost:6379 for Redis-backed assistant integration tests.";

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
    public async Task Chat_WithRepositoryQuestion_UsesEmbeddingsVectorSearchAndRag()
    {
        var embeddingService = new RecordingEmbeddingService();
        var vectorStore = new RecordingVectorStore();
        var metadataProvider = new StubMetadataProvider();

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<FreezerLegoMealsContext>>();
                services.RemoveAll<FreezerLegoMealsContext>();
                services.AddDbContext<FreezerLegoMealsContext>(options =>
                    options.UseInMemoryDatabase("AssistantRagIntegrationTestDatabase"));
                services.RemoveAll<IOllamaClient>();
                services.RemoveAll<IPromptBuilder>();
                services.RemoveAll<IEmbeddingService>();
                services.RemoveAll<IVectorStore>();
                services.RemoveAll<ISemanticRecipeMetadataProvider>();
                services.RemoveAll<IQueryRewriter>();
                services.RemoveAll<IReranker>();
                services.RemoveAll<IAnswerGroundingService>();
                services.AddSingleton<IOllamaClient, NoContextOllamaClient>();
                services.AddSingleton<IPromptBuilder, StubPromptBuilder>();
                services.AddSingleton<IEmbeddingService>(embeddingService);
                services.AddSingleton<IVectorStore>(vectorStore);
                services.AddSingleton<ISemanticRecipeMetadataProvider>(metadataProvider);
                services.AddSingleton<IQueryRewriter>(new StubQueryRewriter("spicy chicken freezer recipes"));
                services.AddSingleton<IAnswerGroundingService>(new StubAnswerGroundingService(true, 0));
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
        Assert.Equal("spicy chicken freezer recipe", embeddingService.LastText);
        Assert.Equal(new[] { 1f, 0f }, vectorStore.LastEmbedding);
        Assert.Equal(3, vectorStore.LastTopK);
    }

    [Fact]
    public async Task Chat_WithPreferredLanguageAndStrictMode_PropagatesLocalizationToRetrievalMetadata()
    {
        var embeddingService = new RecordingEmbeddingService();
        var vectorStore = new RecordingVectorStore();
        var metadataProvider = new LocalizedMetadataProvider(supportsPortuguese: true);

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<FreezerLegoMealsContext>>();
                services.RemoveAll<FreezerLegoMealsContext>();
                services.AddDbContext<FreezerLegoMealsContext>(options =>
                    options.UseInMemoryDatabase("AssistantLocalizationPropagationIntegrationTestDatabase"));
                services.RemoveAll<IOllamaClient>();
                services.RemoveAll<IPromptBuilder>();
                services.RemoveAll<IEmbeddingService>();
                services.RemoveAll<IVectorStore>();
                services.RemoveAll<ISemanticRecipeMetadataProvider>();
                services.RemoveAll<IQueryRewriter>();
                services.RemoveAll<IReranker>();
                services.RemoveAll<IAnswerGroundingService>();
                services.AddSingleton<IOllamaClient, NoContextOllamaClient>();
                services.AddSingleton<IPromptBuilder, StubPromptBuilder>();
                services.AddSingleton<IEmbeddingService>(embeddingService);
                services.AddSingleton<IVectorStore>(vectorStore);
                services.AddSingleton<ISemanticRecipeMetadataProvider>(metadataProvider);
                services.AddSingleton<IQueryRewriter>(new StubQueryRewriter("spicy chicken freezer recipes"));
                services.AddSingleton<IAnswerGroundingService>(new StubAnswerGroundingService(true, 0));
            });
        });

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Accept-Language", "pt-BR,pt;q=0.9,en;q=0.8");
        var response = await client.PostAsJsonAsync("/api/assistant/chat", new AssistantChatRequest
        {
            Message = "What spicy chicken meal can I cook?",
            Language = "pt",
            StrictMode = true
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<AssistantChatResponse>(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.NotNull(payload);
        Assert.Contains("Sources:", payload.Response);
        Assert.Contains("1: Frango Picante", payload.Response);
        Assert.Equal("pt", metadataProvider.LastLocalization?.PreferredLanguage);
        Assert.True(metadataProvider.LastLocalization?.StrictMode);
    }

    [Fact]
    public async Task Chat_WithoutExplicitLanguage_AutoDetectsLanguageFromLatestMessage()
    {
        var embeddingService = new RecordingEmbeddingService();
        var vectorStore = new RecordingVectorStore();
        var metadataProvider = new LocalizedMetadataProvider(supportsPortuguese: true);

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<FreezerLegoMealsContext>>();
                services.RemoveAll<FreezerLegoMealsContext>();
                services.AddDbContext<FreezerLegoMealsContext>(options =>
                    options.UseInMemoryDatabase("AssistantAutoDetectionIntegrationTestDatabase"));
                services.RemoveAll<IOllamaClient>();
                services.RemoveAll<IPromptBuilder>();
                services.RemoveAll<IEmbeddingService>();
                services.RemoveAll<IVectorStore>();
                services.RemoveAll<ISemanticRecipeMetadataProvider>();
                services.RemoveAll<IQueryRewriter>();
                services.RemoveAll<IReranker>();
                services.RemoveAll<IAnswerGroundingService>();
                services.AddSingleton<IOllamaClient, StubOllamaClient>();
                services.AddSingleton<IPromptBuilder, StubPromptBuilder>();
                services.AddSingleton<IEmbeddingService>(embeddingService);
                services.AddSingleton<IVectorStore>(vectorStore);
                services.AddSingleton<ISemanticRecipeMetadataProvider>(metadataProvider);
                services.AddSingleton<IQueryRewriter>(new StubQueryRewriter("frango congelador"));
                services.AddSingleton<IAnswerGroundingService>(new StubAnswerGroundingService(true, 0));
            });
        });

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
        var response = await client.PostAsJsonAsync("/api/assistant/chat", new AssistantChatRequest
        {
            Message = "Que receitas tens com frango?"
        });

        response.EnsureSuccessStatusCode();
        _ = await response.Content.ReadFromJsonAsync<AssistantChatResponse>(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.Equal("pt", metadataProvider.LastLocalization?.PreferredLanguage);
    }

    [Fact]
    public async Task Chat_WithPreferredLanguageAndFallback_UsesFallbackLocalizationMetadata()
    {
        var embeddingService = new RecordingEmbeddingService();
        var vectorStore = new RecordingVectorStore();
        var metadataProvider = new LocalizedMetadataProvider(supportsPortuguese: false);

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<FreezerLegoMealsContext>>();
                services.RemoveAll<FreezerLegoMealsContext>();
                services.AddDbContext<FreezerLegoMealsContext>(options =>
                    options.UseInMemoryDatabase("AssistantLocalizationFallbackIntegrationTestDatabase"));
                services.RemoveAll<IOllamaClient>();
                services.RemoveAll<IPromptBuilder>();
                services.RemoveAll<IEmbeddingService>();
                services.RemoveAll<IVectorStore>();
                services.RemoveAll<ISemanticRecipeMetadataProvider>();
                services.RemoveAll<IQueryRewriter>();
                services.RemoveAll<IReranker>();
                services.RemoveAll<IAnswerGroundingService>();
                services.AddSingleton<IOllamaClient, StubOllamaClient>();
                services.AddSingleton<IPromptBuilder, StubPromptBuilder>();
                services.AddSingleton<IEmbeddingService>(embeddingService);
                services.AddSingleton<IVectorStore>(vectorStore);
                services.AddSingleton<ISemanticRecipeMetadataProvider>(metadataProvider);
                services.AddSingleton<IQueryRewriter>(new StubQueryRewriter("frango congelador"));
                services.AddSingleton<IAnswerGroundingService>(new StubAnswerGroundingService(true, 0));
            });
        });

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Accept-Language", "pt-BR,pt;q=0.9,en;q=0.8");
        var response = await client.PostAsJsonAsync("/api/assistant/chat", new AssistantChatRequest
        {
            Message = "Que receitas tens com frango?",
            StrictMode = false
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<AssistantChatResponse>(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.NotNull(payload);
        Assert.Contains("Sources:", payload.Response);
        Assert.Contains("1: Spicy Chicken", payload.Response);
        Assert.Equal("pt", metadataProvider.LastLocalization?.PreferredLanguage);
        Assert.Contains("pt-BR", metadataProvider.LastLocalization?.FallbackLanguages ?? Array.Empty<string>());
        Assert.Contains("en", metadataProvider.LastLocalization?.FallbackLanguages ?? Array.Empty<string>());
        Assert.False(metadataProvider.LastLocalization?.StrictMode);
    }

    [Fact]
    public async Task Chat_WithStrictMode_AndMissingLocalizedProjection_ReturnsNoSupport()
    {
        var embeddingService = new RecordingEmbeddingService();
        var vectorStore = new RecordingVectorStore();
        var metadataProvider = new LocalizedMetadataProvider(supportsPortuguese: false);

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<FreezerLegoMealsContext>>();
                services.RemoveAll<FreezerLegoMealsContext>();
                services.AddDbContext<FreezerLegoMealsContext>(options =>
                    options.UseInMemoryDatabase("AssistantStrictLocalizationMissIntegrationTestDatabase"));
                services.RemoveAll<IOllamaClient>();
                services.RemoveAll<IPromptBuilder>();
                services.RemoveAll<IEmbeddingService>();
                services.RemoveAll<IVectorStore>();
                services.RemoveAll<ISemanticRecipeMetadataProvider>();
                services.RemoveAll<IQueryRewriter>();
                services.RemoveAll<IReranker>();
                services.RemoveAll<IAnswerGroundingService>();
                services.AddSingleton<IOllamaClient, NoContextOllamaClient>();
                services.AddSingleton<IPromptBuilder, StubPromptBuilder>();
                services.AddSingleton<IEmbeddingService>(embeddingService);
                services.AddSingleton<IVectorStore>(vectorStore);
                services.AddSingleton<ISemanticRecipeMetadataProvider>(metadataProvider);
                services.AddSingleton<IQueryRewriter>(new StubQueryRewriter("frango congelador"));
                services.AddSingleton<IAnswerGroundingService>(new StubAnswerGroundingService(true, 0));
            });
        });

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/assistant/chat", new AssistantChatRequest
        {
            Message = "Que receitas tens com frango?",
            Language = "pt",
            StrictMode = true
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<AssistantChatResponse>(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.NotNull(payload);
        Assert.Contains("repository does not contain enough information", payload!.Response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Sources: none", payload.Response);
        Assert.DoesNotContain("Salsa Verde Chicken", payload.Response, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Chat_WithRepositoryQuestion_WhenQueryRewriteFails_FallsBackToOriginalQuestion()
    {
        var embeddingService = new RecordingEmbeddingService();
        var vectorStore = new RecordingVectorStore();
        var metadataProvider = new StubMetadataProvider();

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<FreezerLegoMealsContext>>();
                services.RemoveAll<FreezerLegoMealsContext>();
                services.AddDbContext<FreezerLegoMealsContext>(options =>
                    options.UseInMemoryDatabase("AssistantRagRewriteFallbackIntegrationTestDatabase"));
                services.RemoveAll<IOllamaClient>();
                services.RemoveAll<IPromptBuilder>();
                services.RemoveAll<IEmbeddingService>();
                services.RemoveAll<IVectorStore>();
                services.RemoveAll<ISemanticRecipeMetadataProvider>();
                services.RemoveAll<IQueryRewriter>();
                services.RemoveAll<IReranker>();
                services.RemoveAll<IAnswerGroundingService>();
                services.AddSingleton<IOllamaClient, StubOllamaClient>();
                services.AddSingleton<IPromptBuilder, StubPromptBuilder>();
                services.AddSingleton<IEmbeddingService>(embeddingService);
                services.AddSingleton<IVectorStore>(vectorStore);
                services.AddSingleton<ISemanticRecipeMetadataProvider>(metadataProvider);
                services.AddSingleton<IQueryRewriter, ThrowingQueryRewriter>();
                services.AddSingleton<IAnswerGroundingService>(new StubAnswerGroundingService(true, 0));
            });
        });

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/assistant/chat", new AssistantChatRequest
        {
            Message = "What spicy chicken meal can I cook?"
        });

        response.EnsureSuccessStatusCode();
        _ = await response.Content.ReadFromJsonAsync<AssistantChatResponse>(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.Equal("what spicy chicken meal can i cook", embeddingService.LastText);
    }

    [Fact]
    public async Task Chat_WithRepositoryQuestion_UsesHybridFusionRanking()
    {
        var embeddingService = new RecordingEmbeddingService();
        var vectorStore = new RecordingVectorStore([
            new VectorMatch("1", 0.95),
            new VectorMatch("3", 0.70)
        ]);
        var metadataProvider = new HybridMetadataProvider();

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<FreezerLegoMealsContext>>();
                services.RemoveAll<FreezerLegoMealsContext>();
                services.AddDbContext<FreezerLegoMealsContext>(options =>
                    options.UseInMemoryDatabase("AssistantHybridRagIntegrationTestDatabase"));
                services.RemoveAll<IOllamaClient>();
                services.RemoveAll<IPromptBuilder>();
                services.RemoveAll<IEmbeddingService>();
                services.RemoveAll<IVectorStore>();
                services.RemoveAll<ISemanticRecipeMetadataProvider>();
                services.RemoveAll<IQueryRewriter>();
                services.RemoveAll<IKeywordSearchService>();
                services.RemoveAll<IReranker>();
                services.RemoveAll<IAnswerGroundingService>();
                services.AddSingleton<IOllamaClient, StubOllamaClient>();
                services.AddSingleton<IPromptBuilder, StubPromptBuilder>();
                services.AddSingleton<IEmbeddingService>(embeddingService);
                services.AddSingleton<IVectorStore>(vectorStore);
                services.AddSingleton<ISemanticRecipeMetadataProvider>(metadataProvider);
                services.AddSingleton<IQueryRewriter>(new StubQueryRewriter("chicken and beef freezer recipes"));
                services.AddSingleton<IKeywordSearchService>(new StubKeywordSearchService([
                    new KeywordSearchResult("2", 3),
                    new KeywordSearchResult("1", 2)
                ]));
                services.AddSingleton<IAnswerGroundingService>(new StubAnswerGroundingService(true, 0));
            });
        });

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/assistant/chat", new AssistantChatRequest
        {
            Message = "What chicken recipes do you have?"
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<AssistantChatResponse>(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.NotNull(payload);
        Assert.Contains("Sources:", payload.Response);

        var sourceOne = payload.Response.IndexOf("- 1: Spicy Chicken", StringComparison.Ordinal);
        var sourceTwo = payload.Response.IndexOf("- 2: Beef Stir Fry", StringComparison.Ordinal);
        var sourceThree = payload.Response.IndexOf("- 3: Garlic Rice", StringComparison.Ordinal);
        Assert.True(sourceOne >= 0);
        Assert.True(sourceTwo >= 0);
        Assert.True(sourceThree >= 0);
        Assert.True(sourceOne < sourceTwo);
        Assert.True(sourceTwo < sourceThree);
        Assert.Equal("chicken and beef freezer recipe", embeddingService.LastText);
        Assert.Equal(3, vectorStore.LastTopK);
    }

    [Fact]
    public async Task Chat_WithRepositoryQuestion_AppliesRerankingBeforeSources()
    {
        var vectorStore = new RecordingVectorStore([
            new VectorMatch("1", 0.95),
            new VectorMatch("2", 0.90),
            new VectorMatch("3", 0.70)
        ]);
        var metadataProvider = new HybridMetadataProvider();

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<FreezerLegoMealsContext>>();
                services.RemoveAll<FreezerLegoMealsContext>();
                services.AddDbContext<FreezerLegoMealsContext>(options =>
                    options.UseInMemoryDatabase("AssistantRerankIntegrationTestDatabase"));
                services.RemoveAll<IOllamaClient>();
                services.RemoveAll<IPromptBuilder>();
                services.RemoveAll<IEmbeddingService>();
                services.RemoveAll<IVectorStore>();
                services.RemoveAll<ISemanticRecipeMetadataProvider>();
                services.RemoveAll<IQueryRewriter>();
                services.RemoveAll<IReranker>();
                services.RemoveAll<IAnswerGroundingService>();
                services.AddSingleton<IOllamaClient, StubOllamaClient>();
                services.AddSingleton<IPromptBuilder, StubPromptBuilder>();
                services.AddSingleton<IEmbeddingService, RecordingEmbeddingService>();
                services.AddSingleton<IVectorStore>(vectorStore);
                services.AddSingleton<ISemanticRecipeMetadataProvider>(metadataProvider);
                services.AddSingleton<IQueryRewriter>(new StubQueryRewriter("chicken freezer meals"));
                services.AddSingleton<IReranker>(new StubReranker(["2", "1", "3"]));
                services.AddSingleton<IAnswerGroundingService>(new StubAnswerGroundingService(true, 0));
            });
        });

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/assistant/chat", new AssistantChatRequest
        {
            Message = "What chicken recipes do you have?"
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<AssistantChatResponse>(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.NotNull(payload);
        var sourceOne = payload.Response.IndexOf("- 2: Beef Stir Fry", StringComparison.Ordinal);
        var sourceTwo = payload.Response.IndexOf("- 1: Spicy Chicken", StringComparison.Ordinal);
        var sourceThree = payload.Response.IndexOf("- 3: Garlic Rice", StringComparison.Ordinal);
        Assert.True(sourceOne >= 0);
        Assert.True(sourceTwo >= 0);
        Assert.True(sourceThree >= 0);
        Assert.True(sourceOne < sourceTwo);
        Assert.True(sourceTwo < sourceThree);
    }

    [Fact]
    public async Task Chat_WithRepositoryQuestion_WhenAnswerIsUngrounded_ReturnsRetrievalBackedFallback()
    {
        var vectorStore = new RecordingVectorStore([
            new VectorMatch("1", 0.95)
        ]);
        var metadataProvider = new StubMetadataProvider();

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<FreezerLegoMealsContext>>();
                services.RemoveAll<FreezerLegoMealsContext>();
                services.AddDbContext<FreezerLegoMealsContext>(options =>
                    options.UseInMemoryDatabase("AssistantGroundingIntegrationTestDatabase"));
                services.RemoveAll<IOllamaClient>();
                services.RemoveAll<IPromptBuilder>();
                services.RemoveAll<IEmbeddingService>();
                services.RemoveAll<IVectorStore>();
                services.RemoveAll<ISemanticRecipeMetadataProvider>();
                services.RemoveAll<IQueryRewriter>();
                services.RemoveAll<IReranker>();
                services.RemoveAll<IAnswerGroundingService>();
                services.AddSingleton<IOllamaClient, UnsupportedClaimOllamaClient>();
                services.AddSingleton<IPromptBuilder, StubPromptBuilder>();
                services.AddSingleton<IEmbeddingService, RecordingEmbeddingService>();
                services.AddSingleton<IVectorStore>(vectorStore);
                services.AddSingleton<ISemanticRecipeMetadataProvider>(metadataProvider);
                services.AddSingleton<IQueryRewriter>(new StubQueryRewriter("spicy chicken meals"));
                services.AddSingleton<IAnswerGroundingService>(new StubAnswerGroundingService(false, 2));
            });
        });

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/assistant/chat", new AssistantChatRequest
        {
            Message = "What chicken recipes do you have?"
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<AssistantChatResponse>(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.NotNull(payload);
        Assert.Contains("repository does not contain enough information", payload.Response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Sources:", payload.Response);
        Assert.DoesNotContain("salmon", payload.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Chat_WithToolRequest_ExecutesToolThroughAssistantPipeline()
    {
        var toolExecutor = new RecordingToolExecutor();

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<FreezerLegoMealsContext>>();
                services.RemoveAll<FreezerLegoMealsContext>();
                services.AddDbContext<FreezerLegoMealsContext>(options =>
                    options.UseInMemoryDatabase("AssistantToolIntegrationTestDatabase"));
                services.RemoveAll<IOllamaClient>();
                services.RemoveAll<IToolExecutor>();
                services.AddSingleton<IOllamaClient, ToolCallingOllamaClient>();
                services.AddSingleton<IToolExecutor>(toolExecutor);
            });
        });

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/assistant/chat", new AssistantChatRequest
        {
            Message = "Use the example tool"
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<AssistantChatResponse>();

        Assert.NotNull(payload);
        Assert.Equal("tool completed", payload.Response);
        Assert.Equal("example_tool", toolExecutor.LastToolName);
        Assert.Equal("hello", toolExecutor.LastParameters?["message"]);
    }

    [Theory]
    [InlineData("/embeddings")]
    [InlineData("/api/embeddings")]
    [InlineData("/api/semantic-search")]
    public async Task InternalAiEndpoints_AreNotExposed(string endpoint)
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(endpoint, new { text = "spicy chicken", query = "spicy chicken" });

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Swagger_ExposesAssistantAsOnlyPublicAiEndpoint()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var swagger = await client.GetFromJsonAsync<JsonDocument>("/swagger/v1/swagger.json");
        var paths = swagger!.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/api/Assistant/chat", out _));
        Assert.False(paths.TryGetProperty("/embeddings", out _));
        Assert.False(paths.TryGetProperty("/api/Embeddings", out _));
        Assert.False(paths.TryGetProperty("/api/semantic-search", out _));
    }

    [Fact]
    public async Task Chat_WithBlankMessage_ReturnsBadRequest()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<FreezerLegoMealsContext>>();
                services.RemoveAll<FreezerLegoMealsContext>();
                services.AddDbContext<FreezerLegoMealsContext>(options =>
                    options.UseInMemoryDatabase($"AssistantValidationIntegrationTestDatabase-{Guid.NewGuid():N}"));
            });
        });

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/assistant/chat", new AssistantChatRequest
        {
            Message = " "
        });

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [RedisAvailableFact]
    public async Task Chat_WithFollowUpAcrossFactories_PersistsConversationThroughRedisMemoryProvider()
    {
        string? conversationId = null;

        try
        {
            using (var firstFactory = CreateRedisAssistantFactory())
            {
                using var scope = firstFactory.Services.CreateScope();
                Assert.IsType<RedisMemoryProvider>(scope.ServiceProvider.GetRequiredService<IConversationStore>());

                using var firstClient = firstFactory.CreateClient();
                var firstResponse = await firstClient.PostAsJsonAsync("/api/assistant/chat", new AssistantChatRequest
                {
                    Message = "first message"
                });

                firstResponse.EnsureSuccessStatusCode();
                var firstPayload = await firstResponse.Content.ReadFromJsonAsync<AssistantChatResponse>(new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                Assert.NotNull(firstPayload);
                conversationId = firstPayload.ConversationId;
                Assert.False(string.IsNullOrWhiteSpace(conversationId));
                Assert.Equal("history:1", firstPayload.Response);
            }

            using (var secondFactory = CreateRedisAssistantFactory())
            {
                using var scope = secondFactory.Services.CreateScope();
                Assert.IsType<RedisMemoryProvider>(scope.ServiceProvider.GetRequiredService<IConversationStore>());

                using var secondClient = secondFactory.CreateClient();
                var secondResponse = await secondClient.PostAsJsonAsync("/api/assistant/chat", new AssistantChatRequest
                {
                    ConversationId = conversationId,
                    Message = "second message"
                });

                secondResponse.EnsureSuccessStatusCode();
                var secondPayload = await secondResponse.Content.ReadFromJsonAsync<AssistantChatResponse>(new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                Assert.NotNull(secondPayload);
                Assert.Equal(conversationId, secondPayload.ConversationId);
                Assert.Equal("history:3", secondPayload.Response);
            }
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(conversationId))
            {
                await DeleteConversationAsync(conversationId);
            }
        }
    }

    [RedisAvailableFact]
    public async Task Chat_WithRedisConversation_CreatesRetrievesAndUpdatesThroughApi()
    {
        string? conversationId = null;

        try
        {
            using var factory = CreateRedisAssistantFactory();
            using var scope = factory.Services.CreateScope();
            Assert.IsType<RedisMemoryProvider>(scope.ServiceProvider.GetRequiredService<IConversationStore>());

            using var client = factory.CreateClient();

            var firstResponse = await client.PostAsJsonAsync("/api/assistant/chat", new AssistantChatRequest
            {
                Message = "first message"
            });
            firstResponse.EnsureSuccessStatusCode();

            var firstPayload = await firstResponse.Content.ReadFromJsonAsync<AssistantChatResponse>(new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            Assert.NotNull(firstPayload);
            conversationId = firstPayload.ConversationId;
            Assert.False(string.IsNullOrWhiteSpace(conversationId));
            Assert.Equal("history:1", firstPayload.Response);

            var secondResponse = await client.PostAsJsonAsync("/api/assistant/chat", new AssistantChatRequest
            {
                ConversationId = conversationId,
                Message = "second message"
            });
            secondResponse.EnsureSuccessStatusCode();

            var secondPayload = await secondResponse.Content.ReadFromJsonAsync<AssistantChatResponse>(new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            Assert.NotNull(secondPayload);
            Assert.Equal(conversationId, secondPayload.ConversationId);
            Assert.Equal("history:3", secondPayload.Response);
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(conversationId))
            {
                await DeleteConversationAsync(conversationId);
            }
        }
    }

    [RedisAvailableFact]
    public async Task Chat_WithRedisConversation_ExpiresByConfiguredTimeout()
    {
        string? conversationId = null;

        try
        {
            using var factory = CreateRedisAssistantFactory(expirationTimeout: TimeSpan.FromSeconds(1));
            using var client = factory.CreateClient();

            var firstResponse = await client.PostAsJsonAsync("/api/assistant/chat", new AssistantChatRequest
            {
                Message = "first message"
            });
            firstResponse.EnsureSuccessStatusCode();

            var firstPayload = await firstResponse.Content.ReadFromJsonAsync<AssistantChatResponse>(new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            Assert.NotNull(firstPayload);
            conversationId = firstPayload.ConversationId;

            await Task.Delay(TimeSpan.FromSeconds(2));

            var secondResponse = await client.PostAsJsonAsync("/api/assistant/chat", new AssistantChatRequest
            {
                ConversationId = conversationId,
                Message = "after expiration"
            });
            secondResponse.EnsureSuccessStatusCode();

            var secondPayload = await secondResponse.Content.ReadFromJsonAsync<AssistantChatResponse>(new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            Assert.NotNull(secondPayload);
            Assert.Equal(conversationId, secondPayload.ConversationId);
            Assert.Equal("history:1", secondPayload.Response);
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(conversationId))
            {
                await DeleteConversationAsync(conversationId);
            }
        }
    }

    [Fact]
    public async Task Chat_WithInvalidRedisConnection_FallsBackAndStillHandlesConversation()
    {
        using var factory = CreateRedisAssistantFactory(redisConnectionString: "localhost:6399,abortConnect=false,connectTimeout=500,syncTimeout=500");
        using var client = factory.CreateClient();

        var firstResponse = await client.PostAsJsonAsync("/api/assistant/chat", new AssistantChatRequest
        {
            Message = "first message"
        });
        firstResponse.EnsureSuccessStatusCode();

        var firstPayload = await firstResponse.Content.ReadFromJsonAsync<AssistantChatResponse>(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.NotNull(firstPayload);
        Assert.Equal("history:1", firstPayload.Response);

        var secondResponse = await client.PostAsJsonAsync("/api/assistant/chat", new AssistantChatRequest
        {
            ConversationId = firstPayload.ConversationId,
            Message = "second message"
        });
        secondResponse.EnsureSuccessStatusCode();

        var secondPayload = await secondResponse.Content.ReadFromJsonAsync<AssistantChatResponse>(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.NotNull(secondPayload);
        Assert.Equal(firstPayload.ConversationId, secondPayload.ConversationId);
        Assert.Equal("history:3", secondPayload.Response);
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

    internal static async Task<RedisAvailability> GetRedisAvailabilityAsync()
    {
        try
        {
            await using var redis = await ConnectionMultiplexer.ConnectAsync(RedisConnectionString);
            await redis.GetDatabase().PingAsync();
            return RedisAvailability.Available();
        }
        catch (Exception ex)
        {
            return RedisAvailability.Unavailable($"{RedisRequiredMessage} Connection failed: {ex.Message}");
        }
    }

    internal sealed record OllamaAvailability(bool IsAvailable, string? Model, string? SkipReason)
    {
        public static OllamaAvailability Available(string model) => new(true, model, null);

        public static OllamaAvailability Unavailable(string skipReason) => new(false, null, skipReason);
    }

    internal sealed record RedisAvailability(bool IsAvailable, string? SkipReason)
    {
        public static RedisAvailability Available() => new(true, null);

        public static RedisAvailability Unavailable(string skipReason) => new(false, skipReason);
    }

    private static WebApplicationFactory<Program> CreateRedisAssistantFactory(
        string? redisConnectionString = null,
        TimeSpan? expirationTimeout = null)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConversationStore:RedisConnectionString"] = redisConnectionString ?? RedisConnectionString,
                    ["ConversationStore:ExpirationTimeout"] = (expirationTimeout ?? TimeSpan.FromHours(1)).ToString("c")
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<FreezerLegoMealsContext>>();
                services.RemoveAll<FreezerLegoMealsContext>();
                services.AddDbContext<FreezerLegoMealsContext>(options =>
                    options.UseInMemoryDatabase($"AssistantRedisIntegrationTestDatabase-{Guid.NewGuid():N}"));
                services.RemoveAll<IAssistantOrchestrator>();
                services.AddSingleton<IAssistantOrchestrator, ConversationCountingOrchestrator>();
            });
        });
    }

    private static async Task DeleteConversationAsync(string conversationId)
    {
        await using var redis = await ConnectionMultiplexer.ConnectAsync(RedisConnectionString);
        await redis.GetDatabase().KeyDeleteAsync($"conversation:{conversationId}");
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

    private sealed class RecordingEmbeddingService : IEmbeddingService
    {
        public string? LastText { get; private set; }

        public Task<EmbeddingResponse> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            LastText = text;
            return Task.FromResult(new EmbeddingResponse("test", 2, [1f, 0f]));
        }
    }

    private sealed class RecordingVectorStore : IVectorStore
    {
        private readonly IReadOnlyList<VectorMatch> _matches;

        public RecordingVectorStore(IReadOnlyList<VectorMatch>? matches = null)
        {
            _matches = matches ?? [new VectorMatch("1", 0.91)];
        }

        public IReadOnlyList<float>? LastEmbedding { get; private set; }
        public int? LastTopK { get; private set; }

        public Task<IReadOnlyList<VectorMatch>> SearchAsync(IReadOnlyList<float> queryEmbedding, int topK, CancellationToken cancellationToken = default)
        {
            LastEmbedding = queryEmbedding;
            LastTopK = topK;
            return Task.FromResult<IReadOnlyList<VectorMatch>>(_matches.Take(topK).ToList());
        }

        public Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpsertAsync(IReadOnlyList<VectorDocument> documents, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteAsync(IReadOnlyList<string> recipeIds, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ClearCollectionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubMetadataProvider : ISemanticRecipeMetadataProvider
    {
        public Task<RecipeMetadata> GetMetadataAsync(string recipeId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RecipeMetadata(recipeId, "Spicy Chicken", "spicy chicken dinner", "Dinner", "spicy", ["chicken"], "Slice", "45"));
    }

    private sealed class LocalizedMetadataProvider : ISemanticRecipeMetadataProvider, ILocalizedSemanticRecipeMetadataProvider
    {
        private readonly bool _supportsPortuguese;

        public LocalizedMetadataProvider(bool supportsPortuguese)
        {
            _supportsPortuguese = supportsPortuguese;
        }

        public LocalizationOptions? LastLocalization { get; private set; }

        public Task<RecipeMetadata> GetMetadataAsync(string recipeId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new RecipeMetadata(recipeId, "Spicy Chicken", "spicy chicken dinner", "Dinner", "spicy", ["chicken"], "Slice", "45"));
        }

        public Task<RecipeMetadata?> GetMetadataAsync(
            string recipeId,
            LocalizationOptions localizationOptions,
            CancellationToken cancellationToken = default)
        {
            LastLocalization = localizationOptions;

            if (localizationOptions.PreferredLanguage.StartsWith("pt", StringComparison.OrdinalIgnoreCase) && _supportsPortuguese)
            {
                return Task.FromResult<RecipeMetadata?>(new RecipeMetadata(recipeId, "Frango Picante", "frango picante", "Jantar", "picante", ["frango"], "Cozinhar", "45"));
            }

            if (localizationOptions.StrictMode)
            {
                return Task.FromResult<RecipeMetadata?>(null);
            }

            return Task.FromResult<RecipeMetadata?>(new RecipeMetadata(recipeId, "Spicy Chicken", "spicy chicken dinner", "Dinner", "spicy", ["chicken"], "Slice", "45"));
        }
    }

    private sealed class HybridMetadataProvider : ISemanticRecipeMetadataProvider
    {
        public Task<RecipeMetadata> GetMetadataAsync(string recipeId, CancellationToken cancellationToken = default)
        {
            var metadata = recipeId switch
            {
                "1" => new RecipeMetadata("1", "Spicy Chicken", "spicy chicken dinner", "Dinner", "spicy", ["chicken"], "Slice", "45"),
                "2" => new RecipeMetadata("2", "Beef Stir Fry", "beef stir fry", "Dinner", "beef", ["beef"], "Stir fry", "30"),
                "3" => new RecipeMetadata("3", "Garlic Rice", "garlic rice", "Side", "rice", ["rice"], "Boil", "20"),
                _ => new RecipeMetadata(recipeId, $"Recipe {recipeId}", string.Empty)
            };

            return Task.FromResult(metadata);
        }
    }

    private sealed class StubPromptBuilder : IPromptBuilder
    {
        public string Build(
            string question,
            IReadOnlyList<RetrievalRecipe> recipes,
            string? intentType,
            LocalizationOptions localizationOptions,
            string? requestedLanguage = null) => "rag prompt";
    }

    private sealed class StubQueryRewriter : IQueryRewriter
    {
        private readonly string _rewritten;

        public StubQueryRewriter(string rewritten)
        {
            _rewritten = rewritten;
        }

        public Task<string> RewriteAsync(string query, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_rewritten);
        }
    }

    private sealed class ThrowingQueryRewriter : IQueryRewriter
    {
        public Task<string> RewriteAsync(string query, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("query rewrite failure");
        }
    }

    private sealed class StubKeywordSearchService : IKeywordSearchService
    {
        private readonly IReadOnlyList<KeywordSearchResult> _results;

        public StubKeywordSearchService(IReadOnlyList<KeywordSearchResult> results)
        {
            _results = results;
        }

        public Task<IReadOnlyList<KeywordSearchResult>> SearchAsync(string query, int topK, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<KeywordSearchResult>>(_results.Take(topK).ToList());
        }
    }

    private sealed class StubReranker : IReranker
    {
        private readonly IReadOnlyList<string> _order;

        public StubReranker(IReadOnlyList<string> order)
        {
            _order = order;
        }

        public Task<IReadOnlyList<RetrievalRecipe>> RerankAsync(string query, IReadOnlyList<RetrievalRecipe> candidates, CancellationToken cancellationToken = default)
        {
            var byRecipeId = candidates.ToDictionary(candidate => candidate.RecipeId, StringComparer.Ordinal);
            var ordered = _order
                .Where(recipeId => byRecipeId.ContainsKey(recipeId))
                .Select(recipeId => byRecipeId[recipeId])
                .ToList();

            return Task.FromResult<IReadOnlyList<RetrievalRecipe>>(ordered);
        }
    }

    private sealed class StubAnswerGroundingService : IAnswerGroundingService
    {
        private readonly AnswerGroundingResult _result;

        public StubAnswerGroundingService(bool grounded, int unsupportedClaimsCount)
        {
            _result = new AnswerGroundingResult(grounded, unsupportedClaimsCount);
        }

        public Task<AnswerGroundingResult> ValidateAsync(string answer, IReadOnlyList<RetrievalRecipe> retrievedRecipes, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_result);
        }
    }

    private sealed class UnsupportedClaimOllamaClient : IOllamaClient
    {
        private int _calls;

        public Task<OllamaChatResult> ChatAsync(string? model, IReadOnlyList<ConversationMessage> messages, IReadOnlyList<ToolDefinition> tools, CancellationToken cancellationToken = default)
        {
            _calls++;
            return Task.FromResult(_calls switch
            {
                1 => new OllamaChatResult("draft response", []),
                2 => new OllamaChatResult("This meal includes salmon and quinoa.", []),
                _ => new OllamaChatResult("The repository does not contain enough information to answer that question.", [])
            });
        }
    }

    private sealed class NoContextOllamaClient : IOllamaClient
    {
        private int _calls;

        public Task<OllamaChatResult> ChatAsync(string? model, IReadOnlyList<ConversationMessage> messages, IReadOnlyList<ToolDefinition> tools, CancellationToken cancellationToken = default)
        {
            _calls++;
            return Task.FromResult(_calls == 1
                ? new OllamaChatResult("draft response", [])
                : new OllamaChatResult("The repository does not contain enough information to answer that question.", []));
        }
    }

    private sealed class ToolCallingOllamaClient : IOllamaClient
    {
        private int _calls;

        public Task<OllamaChatResult> ChatAsync(string? model, IReadOnlyList<ConversationMessage> messages, IReadOnlyList<ToolDefinition> tools, CancellationToken cancellationToken = default)
        {
            _calls++;
            return Task.FromResult(_calls == 1
                ? new OllamaChatResult("", [new AssistantToolCall("example_tool", new Dictionary<string, object?> { ["message"] = "hello" })])
                : new OllamaChatResult("tool completed", []));
        }
    }

    private sealed class RecordingToolExecutor : IToolExecutor
    {
        public string? LastToolName { get; private set; }
        public IReadOnlyDictionary<string, object?>? LastParameters { get; private set; }

        public IReadOnlyList<ToolDefinition> GetTools() =>
            [new ToolDefinition { Name = "example_tool", Description = "Example tool" }];

        public Task<ToolExecutionResult> ExecuteAsync(string toolName, IReadOnlyDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default)
        {
            LastToolName = toolName;
            LastParameters = parameters;
            return Task.FromResult(new ToolExecutionResult { Success = true, Tool = toolName, Output = new { ok = true } });
        }
    }

    private sealed class ConversationCountingOrchestrator : IAssistantOrchestrator
    {
        public Task<OrchestratorResult> ExecuteAsync(OrchestratorContext context, CancellationToken cancellationToken = default)
        {
            var response = $"history:{context.Messages.Count(message => message.Role != ConversationRole.System)}";
            var assistantMessage = new ConversationMessage(ConversationRole.Assistant, response, DateTimeOffset.UtcNow);
            var messagesToPersist = context.MessagesToPersist.Concat([assistantMessage]).ToList();

            return Task.FromResult(new OrchestratorResult(
                response,
                "ConversationCountingOrchestrator",
                [],
                [],
                ["Assistant", "ConversationCountingOrchestrator"],
                TimeSpan.Zero,
                [],
                messagesToPersist));
        }
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

public sealed class RedisAvailableFactAttribute : FactAttribute
{
    public RedisAvailableFactAttribute()
    {
        var availability = AssistantControllerIntegrationTests.GetRedisAvailabilityAsync().GetAwaiter().GetResult();
        if (!availability.IsAvailable)
        {
            Skip = availability.SkipReason;
        }
    }
}