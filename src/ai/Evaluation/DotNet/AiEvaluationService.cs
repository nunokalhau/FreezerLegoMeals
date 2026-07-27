using SemanticSearch.DotNet;
using Services.DotNet;

namespace Evaluation.DotNet;

public interface IAiEvaluationService
{
    Task<AiEvaluationReport> EvaluateScenarioAsync(AiEvaluationScenario scenario, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiEvaluationReport>> EvaluateDefaultScenariosAsync(CancellationToken cancellationToken = default);
}

public sealed class AiEvaluationService : IAiEvaluationService
{
    private readonly IAssistantService _assistantService;
    private readonly SemanticSearchService _semanticSearchService;
    private readonly IAiEvaluationScenarioCatalog _scenarioCatalog;
    private readonly IAiEvaluationTraceContext _traceContext;
    private readonly IReadOnlyList<IAiEvaluationDimension> _dimensions;

    public AiEvaluationService(
        IAssistantService assistantService,
        SemanticSearchService semanticSearchService,
        IAiEvaluationScenarioCatalog scenarioCatalog,
        IAiEvaluationTraceContext traceContext,
        IEnumerable<IAiEvaluationDimension> dimensions)
    {
        _assistantService = assistantService ?? throw new ArgumentNullException(nameof(assistantService));
        _semanticSearchService = semanticSearchService ?? throw new ArgumentNullException(nameof(semanticSearchService));
        _scenarioCatalog = scenarioCatalog ?? throw new ArgumentNullException(nameof(scenarioCatalog));
        _traceContext = traceContext ?? throw new ArgumentNullException(nameof(traceContext));
        _dimensions = dimensions?.ToArray() ?? throw new ArgumentNullException(nameof(dimensions));

        if (_dimensions.Count == 0)
            throw new ArgumentException("At least one AI evaluation dimension is required.", nameof(dimensions));
    }

    public async Task<IReadOnlyList<AiEvaluationReport>> EvaluateDefaultScenariosAsync(CancellationToken cancellationToken = default)
    {
        var scenarios = _scenarioCatalog.GetDefaultScenarios();
        var reports = new List<AiEvaluationReport>(scenarios.Count);
        foreach (var scenario in scenarios)
        {
            reports.Add(await EvaluateScenarioAsync(scenario, cancellationToken));
        }

        return reports;
    }

    public async Task<AiEvaluationReport> EvaluateScenarioAsync(AiEvaluationScenario scenario, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ValidateScenario(scenario);

        _traceContext.StartScenario(scenario.MockedLlmResponses, scenario.MockedToolResults);

        string? conversationId = null;
        AssistantChatResult? lastChatResult = null;
        foreach (var message in scenario.UserMessages)
        {
            lastChatResult = await _assistantService.ChatAsync(message, conversationId, cancellationToken);
            conversationId = lastChatResult.ConversationId;
        }

        var resolvedLastChat = lastChatResult ?? throw new InvalidOperationException("Scenario execution did not produce a chat result.");
        var lastOrchestratorResult = _traceContext.LastOrchestratorResult
            ?? throw new InvalidOperationException("Scenario execution did not capture orchestration details.");

        var semanticResults = Array.Empty<SemanticSearchResult>();
        if (!string.IsNullOrWhiteSpace(scenario.SemanticProbeQuery))
        {
            semanticResults = (await _semanticSearchService.SearchAsync(scenario.SemanticProbeQuery, 3, cancellationToken)).ToArray();
        }

        var lastOllamaMessages = _traceContext.OllamaInvocations.LastOrDefault()?.Messages ?? [];
        var execution = new AiEvaluationExecution
        {
            LastChatResult = resolvedLastChat,
            LastOrchestratorResult = lastOrchestratorResult,
            OllamaInvocations = _traceContext.OllamaInvocations.ToArray(),
            ToolInvocations = _traceContext.ToolInvocations.ToArray(),
            SemanticSearchResults = semanticResults,
            LastRetrievalResult = _traceContext.LastRetrievalResult,
            LastOllamaMessages = lastOllamaMessages
        };

        var context = new AiEvaluationContext
        {
            Scenario = scenario,
            Execution = execution
        };

        var dimensionResults = _dimensions
            .Select(dimension => dimension.Evaluate(context))
            .ToArray();

        return new AiEvaluationReport(scenario.Id, scenario.Description, dimensionResults, execution);
    }

    private static void ValidateScenario(AiEvaluationScenario scenario)
    {
        if (string.IsNullOrWhiteSpace(scenario.Id))
            throw new ArgumentException("Scenario id is required.", nameof(scenario));

        if (scenario.UserMessages is null || scenario.UserMessages.Count == 0)
            throw new ArgumentException("Scenario must contain at least one user message.", nameof(scenario));

        if (scenario.UserMessages.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Scenario user messages cannot be null or whitespace.", nameof(scenario));
    }
}
