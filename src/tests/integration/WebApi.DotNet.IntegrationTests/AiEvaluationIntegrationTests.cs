using Embedding.DotNet;
using Evaluation.DotNet;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using SemanticSearch.DotNet;
using Services.DotNet;
using VectorStores.DotNet;
using Xunit;

namespace WebApi.DotNet.IntegrationTests;

[Collection("IntegrationTests")]
public sealed class AiEvaluationIntegrationTests
{
    [Fact]
    public async Task EvaluateDefaultScenarios_WithDeterministicInfrastructure_PassesAllQualityGates()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<FreezerLegoMealsContext>>();
                services.RemoveAll<FreezerLegoMealsContext>();
                services.AddDbContext<FreezerLegoMealsContext>(options => options.UseInMemoryDatabase($"AiEvaluation-{Guid.NewGuid():N}"));

                services.RemoveAll<IEmbeddingService>();
                services.RemoveAll<IVectorStore>();
                services.RemoveAll<ISemanticRecipeMetadataProvider>();
                services.AddSingleton<IEmbeddingService, DeterministicEmbeddingService>();
                services.AddSingleton<IVectorStore, DeterministicVectorStore>();
                services.AddSingleton<ISemanticRecipeMetadataProvider, DeterministicMetadataProvider>();

                services.RemoveAll<IConversationStore>();
                services.AddSingleton<IConversationStore>(serviceProvider =>
                    new InMemoryConversationStore(serviceProvider.GetRequiredService<IOptions<ConversationStoreOptions>>()));
            });
        });

        using var scope = factory.Services.CreateScope();
        var evaluationService = scope.ServiceProvider.GetRequiredService<IAiEvaluationService>();

        var reports = await evaluationService.EvaluateDefaultScenariosAsync();

        Assert.NotEmpty(reports);
        var failures = reports
            .SelectMany(report => report.DimensionResults
                .Where(result => result.Status == AiEvaluationStatus.Failed)
                .Select(result => $"scenario={report.ScenarioId};dimension={result.Dimension};details={result.Details}"))
            .ToArray();

        Assert.True(failures.Length == 0, "AI evaluation failures:\n" + string.Join("\n", failures));

        var coveredDimensions = reports
            .SelectMany(report => report.DimensionResults)
            .Where(result => result.Status == AiEvaluationStatus.Passed)
            .Select(result => result.Dimension)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("routing", coveredDimensions);
        Assert.Contains("retrieval-relevance", coveredDimensions);
        Assert.Contains("semantic-search-quality", coveredDimensions);
        Assert.Contains("tool-selection", coveredDimensions);
        Assert.Contains("tool-execution", coveredDimensions);
        Assert.Contains("memory-retrieval", coveredDimensions);
        Assert.Contains("grounded-response", coveredDimensions);
        Assert.Contains("overall-answer-quality", coveredDimensions);
    }

    [OllamaAvailableFact]
    public async Task EvaluateScenario_WithRealOllama_SupportsRealEndToEndMode()
    {
        var availability = await AssistantControllerIntegrationTests.GetOllamaAvailabilityAsync();
        if (!availability.IsAvailable)
            return;

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<FreezerLegoMealsContext>>();
                services.RemoveAll<FreezerLegoMealsContext>();
                services.AddDbContext<FreezerLegoMealsContext>(options => options.UseInMemoryDatabase($"AiEvaluationReal-{Guid.NewGuid():N}"));
            });

            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Ollama:BaseUrl"] = "http://localhost:11434",
                    ["Ollama:DefaultModel"] = availability.Model,
                    ["Ollama:Timeout"] = "00:01:00"
                });
            });
        });

        using var scope = factory.Services.CreateScope();
        var evaluationService = scope.ServiceProvider.GetRequiredService<IAiEvaluationService>();

        var scenario = new AiEvaluationScenario
        {
            Id = "real-ollama-smoke",
            Description = "Smoke test for real end-to-end evaluation mode.",
            UserMessages = ["Reply with the exact token OK"],
            Expectations = new AiEvaluationExpectations
            {
                RequiredResponseFragments = ["ok"],
                ExpectNoErrors = true
            }
        };

        var report = await evaluationService.EvaluateScenarioAsync(scenario);

        Assert.True(report.Passed, string.Join(" | ", report.DimensionResults.Select(result => $"{result.Dimension}:{result.Status}:{result.Details}")));
        Assert.False(string.IsNullOrWhiteSpace(report.Execution.LastChatResult.Response));
    }

    private sealed class DeterministicEmbeddingService : IEmbeddingService
    {
        public Task<EmbeddingResponse> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            var normalized = text.ToLowerInvariant();
            var embedding = normalized.Contains("spicy", StringComparison.Ordinal) || normalized.Contains("chicken", StringComparison.Ordinal)
                ? new[] { 1f, 0f }
                : normalized.Contains("rice", StringComparison.Ordinal)
                    ? new[] { 0f, 1f }
                    : new[] { 0.4f, 0.2f };

            return Task.FromResult(new EmbeddingResponse("deterministic", 2, embedding));
        }
    }

    private sealed class DeterministicVectorStore : IVectorStore
    {
        public Task<IReadOnlyList<VectorMatch>> SearchAsync(IReadOnlyList<float> queryEmbedding, int topK, CancellationToken cancellationToken = default)
        {
            var corpus = new[]
            {
                new VectorMatch("1", CosineSimilarity.Calculate(queryEmbedding, new[] { 1f, 0f })),
                new VectorMatch("2", CosineSimilarity.Calculate(queryEmbedding, new[] { 0f, 1f })),
                new VectorMatch("3", CosineSimilarity.Calculate(queryEmbedding, new[] { 0.4f, 0.2f }))
            };

            return Task.FromResult<IReadOnlyList<VectorMatch>>(corpus
                .OrderByDescending(item => item.Score)
                .Take(topK)
                .ToArray());
        }
    }

    private sealed class DeterministicMetadataProvider : ISemanticRecipeMetadataProvider
    {
        public Task<RecipeMetadata> GetMetadataAsync(string recipeId, CancellationToken cancellationToken = default)
        {
            var metadata = recipeId switch
            {
                "1" => new RecipeMetadata("1", "Spicy Chicken Bowl", "spicy chicken", "desc", "tags", ["chicken", "peppers"], "prep", "45"),
                "2" => new RecipeMetadata("2", "Garlic Rice", "garlic rice", "desc", "tags", ["rice"], "prep", "30"),
                _ => new RecipeMetadata(recipeId, "Veg Prep", "veg", "desc", "tags", ["carrot"], "prep", "35")
            };

            return Task.FromResult(metadata);
        }
    }
}
