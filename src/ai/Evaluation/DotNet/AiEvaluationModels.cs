using Orchestration.DotNet;
using RAG.DotNet;
using SemanticSearch.DotNet;
using Services.DotNet;

namespace Evaluation.DotNet;

public enum AiEvaluationStatus
{
    Passed,
    Failed,
    NotApplicable
}

public sealed record AiEvaluationDimensionResult(string Dimension, AiEvaluationStatus Status, string Details)
{
    public bool IsSuccess => Status is AiEvaluationStatus.Passed or AiEvaluationStatus.NotApplicable;
}

public sealed record AiEvaluationReport(
    string ScenarioId,
    string Description,
    IReadOnlyList<AiEvaluationDimensionResult> DimensionResults,
    AiEvaluationExecution Execution)
{
    public bool Passed => DimensionResults.All(result => result.IsSuccess);
}

public sealed class AiEvaluationExpectations
{
    public AssistantRoute? ExpectedRoute { get; init; }

    public IReadOnlyList<string> ExpectedToolSelection { get; init; } = [];

    public bool? ExpectSuccessfulToolExecution { get; init; }

    public string? ExpectedTopSemanticRecipeId { get; init; }

    public string? ExpectedRetrievedRecipeId { get; init; }

    public bool? ExpectGroundedResponse { get; init; }

    public bool ExpectNoErrors { get; init; } = true;

    public IReadOnlyList<string> RequiredResponseFragments { get; init; } = [];

    public bool? ExpectMemoryRecall { get; init; }

    public int? MinimumSecondTurnMessageCount { get; init; }

    public string? RequiredPriorUserMessageInSecondTurn { get; init; }
}

public sealed class AiEvaluationScenario
{
    public required string Id { get; init; }

    public required string Description { get; init; }

    public required IReadOnlyList<string> UserMessages { get; init; }

    public string? SemanticProbeQuery { get; init; }

    public IReadOnlyList<OllamaChatResult> MockedLlmResponses { get; init; } = [];

    public IReadOnlyDictionary<string, ToolExecutionResult> MockedToolResults { get; init; }
        = new Dictionary<string, ToolExecutionResult>(StringComparer.Ordinal);

    public required AiEvaluationExpectations Expectations { get; init; }
}

public sealed class AiEvaluationExecution
{
    public required AssistantChatResult LastChatResult { get; init; }

    public required OrchestratorResult LastOrchestratorResult { get; init; }

    public required IReadOnlyList<OllamaInvocation> OllamaInvocations { get; init; }

    public required IReadOnlyList<ToolInvocation> ToolInvocations { get; init; }

    public required IReadOnlyList<SemanticSearchResult> SemanticSearchResults { get; init; }

    public required RetrievalResult? LastRetrievalResult { get; init; }

    public required IReadOnlyList<ConversationMessage> LastOllamaMessages { get; init; }
}

public sealed class AiEvaluationContext
{
    public required AiEvaluationScenario Scenario { get; init; }

    public required AiEvaluationExecution Execution { get; init; }
}

public sealed record OllamaInvocation(IReadOnlyList<ConversationMessage> Messages, IReadOnlyList<ToolDefinition> Tools);

public sealed record ToolInvocation(string ToolName, IReadOnlyDictionary<string, object?> Parameters, ToolExecutionResult Result);

public interface IAiEvaluationScenarioCatalog
{
    IReadOnlyList<AiEvaluationScenario> GetDefaultScenarios();
}
