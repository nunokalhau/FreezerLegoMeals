using Embedding.DotNet;
using Evaluation.DotNet;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orchestration.DotNet;
using RAG.DotNet;
using SemanticSearch.DotNet;
using Services.DotNet;
using VectorStores.DotNet;
using Xunit;

namespace Services.DotNet.UnitTests;

public sealed class AiEvaluationServiceTests
{
    [Fact]
    public async Task EvaluateScenarioAsync_WithMockedResponses_ProducesPassingReport()
    {
        var traceContext = new AiEvaluationTraceContext();
        var dimensions = CreateDimensions();
        var scenarioCatalog = new DefaultAiEvaluationScenarioCatalog();

        var semanticSearchService = new SemanticSearchService(
            new DeterministicEmbeddingService(),
            new DeterministicVectorStore(),
            new DeterministicMetadataProvider(),
            NullLogger<SemanticSearchService>.Instance);

        var retrieval = new EvaluationRetrievalService(
            new RetrievalService(
                semanticSearchService,
                new DeterministicMetadataProvider(),
                topK: 3,
                minimumSimilarity: 0.2,
                logger: NullLogger<RetrievalService>.Instance),
            traceContext);

        var toolExecutor = new EvaluationToolExecutor(
            new StubToolExecutor(),
            traceContext);

        var ollama = new EvaluationOllamaClient(new FailingOllamaClient(), traceContext);
        var agent = new MealPlanningAgent(
            ollama,
            toolExecutor,
            new DefaultRoutingPolicy(),
            NullLogger<MealPlanningAgent>.Instance,
            retrieval,
            new PromptBuilder("Context:\n{recipes}\nQuestion:\n{question}"));

        var orchestrator = new EvaluationAssistantOrchestrator(
            new AssistantOrchestrator([agent], new DefaultRoutingPolicy(), NullLogger<AssistantOrchestrator>.Instance),
            traceContext);

        var assistantService = new AssistantService(
            new InMemoryConversationStore(Options.Create(new ConversationStoreOptions())),
            orchestrator,
            Options.Create(new AssistantOptions { SystemPrompt = "test system prompt" }),
            NullLogger<AssistantService>.Instance);

        var service = new AiEvaluationService(
            assistantService,
            semanticSearchService,
            scenarioCatalog,
            traceContext,
            dimensions);

        var scenario = scenarioCatalog.GetDefaultScenarios().First();
        var report = await service.EvaluateScenarioAsync(scenario);

        Assert.True(report.Passed, string.Join(" | ", report.DimensionResults.Select(result => $"{result.Dimension}:{result.Status}:{result.Details}")));
        Assert.Equal("tool-routing-and-execution", report.ScenarioId);
        Assert.Contains(report.DimensionResults, result => result.Dimension == "routing" && result.Status == AiEvaluationStatus.Passed);
        Assert.Contains(report.DimensionResults, result => result.Dimension == "tool-selection" && result.Status == AiEvaluationStatus.Passed);
    }

    private static IReadOnlyList<IAiEvaluationDimension> CreateDimensions()
    {
        return
        [
            new RoutingDecisionEvaluationDimension(),
            new RetrievalRelevanceEvaluationDimension(),
            new SemanticSearchQualityEvaluationDimension(),
            new ToolSelectionEvaluationDimension(),
            new ToolExecutionEvaluationDimension(),
            new MemoryRetrievalEvaluationDimension(),
            new GroundedResponseEvaluationDimension(),
            new OverallAnswerQualityEvaluationDimension()
        ];
    }

    private sealed class StubToolExecutor : IToolExecutor
    {
        public IReadOnlyList<ToolDefinition> GetTools() =>
        [
            new ToolDefinition { Name = "search_recipes", Description = "search" }
        ];

        public Task<ToolExecutionResult> ExecuteAsync(string toolName, IReadOnlyDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ToolExecutionResult
            {
                Success = true,
                Tool = toolName,
                Output = new { ok = true }
            });
        }
    }

    private sealed class FailingOllamaClient : IOllamaClient
    {
        public Task<OllamaChatResult> ChatAsync(string? model, IReadOnlyList<ConversationMessage> messages, IReadOnlyList<ToolDefinition> tools, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("This inner client should not be called when mocked LLM responses are provided.");
        }
    }

    private sealed class DeterministicEmbeddingService : IEmbeddingService
    {
        public Task<EmbeddingResponse> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            var normalized = text.ToLowerInvariant();
            var embedding = normalized.Contains("spicy", StringComparison.Ordinal) || normalized.Contains("chicken", StringComparison.Ordinal)
                ? new[] { 1f, 0f }
                : new[] { 0f, 1f };

            return Task.FromResult(new EmbeddingResponse("deterministic", 2, embedding));
        }
    }

    private sealed class DeterministicVectorStore : IVectorStore
    {
        public Task<IReadOnlyList<VectorMatch>> SearchAsync(IReadOnlyList<float> queryEmbedding, int topK, CancellationToken cancellationToken = default)
        {
            var results = new[]
            {
                new VectorMatch("1", CosineSimilarity.Calculate(queryEmbedding, new[] { 1f, 0f })),
                new VectorMatch("2", CosineSimilarity.Calculate(queryEmbedding, new[] { 0f, 1f }))
            }
            .OrderByDescending(match => match.Score)
            .Take(topK)
            .ToArray();

            return Task.FromResult<IReadOnlyList<VectorMatch>>(results);
        }
    }

    private sealed class DeterministicMetadataProvider : ISemanticRecipeMetadataProvider
    {
        public Task<RecipeMetadata> GetMetadataAsync(string recipeId, CancellationToken cancellationToken = default)
        {
            var metadata = recipeId == "1"
                ? new RecipeMetadata("1", "Spicy Chicken Bowl", "spicy chicken", "desc", "tags", ["chicken"], "prep", "45")
                : new RecipeMetadata("2", "Garlic Rice", "garlic rice", "desc", "tags", ["rice"], "prep", "30");
            return Task.FromResult(metadata);
        }
    }
}
