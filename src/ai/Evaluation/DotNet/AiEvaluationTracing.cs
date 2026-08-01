using Orchestration.DotNet;
using RAG.DotNet;
using Services.DotNet;

namespace Evaluation.DotNet;

public interface IAiEvaluationTraceContext
{
    OrchestratorResult? LastOrchestratorResult { get; }

    RetrievalResult? LastRetrievalResult { get; }

    IReadOnlyList<OllamaInvocation> OllamaInvocations { get; }

    IReadOnlyList<ToolInvocation> ToolInvocations { get; }

    bool HasScriptedLlmResponses { get; }

    bool HasScriptedToolResults { get; }

    void StartScenario(IReadOnlyList<OllamaChatResult> scriptedLlmResponses, IReadOnlyDictionary<string, ToolExecutionResult> scriptedToolResults);

    void Reset();

    bool TryDequeueScriptedLlmResponse(out OllamaChatResult response);

    bool TryGetScriptedToolResult(string toolName, out ToolExecutionResult? result);

    void SetLastOrchestratorResult(OrchestratorResult result);

    void SetLastRetrievalResult(RetrievalResult result);

    void CaptureOllamaInvocation(IReadOnlyList<ConversationMessage> messages, IReadOnlyList<ToolDefinition> tools);

    void CaptureToolInvocation(string toolName, IReadOnlyDictionary<string, object?> parameters, ToolExecutionResult result);
}

public sealed class AiEvaluationTraceContext : IAiEvaluationTraceContext
{
    private readonly List<OllamaInvocation> _ollamaInvocations = [];
    private readonly List<ToolInvocation> _toolInvocations = [];
    private Queue<OllamaChatResult> _scriptedLlmResponses = new();
    private Dictionary<string, ToolExecutionResult> _scriptedToolResults = new(StringComparer.Ordinal);

    public OrchestratorResult? LastOrchestratorResult { get; private set; }

    public RetrievalResult? LastRetrievalResult { get; private set; }

    public IReadOnlyList<OllamaInvocation> OllamaInvocations => _ollamaInvocations;

    public IReadOnlyList<ToolInvocation> ToolInvocations => _toolInvocations;

    public bool HasScriptedLlmResponses => _scriptedLlmResponses.Count > 0;

    public bool HasScriptedToolResults => _scriptedToolResults.Count > 0;

    public void StartScenario(IReadOnlyList<OllamaChatResult> scriptedLlmResponses, IReadOnlyDictionary<string, ToolExecutionResult> scriptedToolResults)
    {
        _scriptedLlmResponses = new Queue<OllamaChatResult>(scriptedLlmResponses ?? []);
        _scriptedToolResults = scriptedToolResults is null
            ? new Dictionary<string, ToolExecutionResult>(StringComparer.Ordinal)
            : new Dictionary<string, ToolExecutionResult>(scriptedToolResults, StringComparer.Ordinal);
        _ollamaInvocations.Clear();
        _toolInvocations.Clear();
        LastOrchestratorResult = null;
        LastRetrievalResult = null;
    }

    public void Reset()
    {
        _scriptedLlmResponses.Clear();
        _scriptedToolResults.Clear();
        _ollamaInvocations.Clear();
        _toolInvocations.Clear();
        LastOrchestratorResult = null;
        LastRetrievalResult = null;
    }

    public bool TryDequeueScriptedLlmResponse(out OllamaChatResult response)
    {
        if (_scriptedLlmResponses.Count == 0)
        {
            response = new OllamaChatResult(string.Empty, []);
            return false;
        }

        response = _scriptedLlmResponses.Dequeue();
        return true;
    }

    public bool TryGetScriptedToolResult(string toolName, out ToolExecutionResult? result)
    {
        var found = _scriptedToolResults.TryGetValue(toolName, out var scripted);
        result = scripted;
        return found;
    }

    public void SetLastOrchestratorResult(OrchestratorResult result)
    {
        LastOrchestratorResult = result;
    }

    public void SetLastRetrievalResult(RetrievalResult result)
    {
        LastRetrievalResult = result;
    }

    public void CaptureOllamaInvocation(IReadOnlyList<ConversationMessage> messages, IReadOnlyList<ToolDefinition> tools)
    {
        _ollamaInvocations.Add(new OllamaInvocation(messages.ToArray(), tools.ToArray()));
    }

    public void CaptureToolInvocation(string toolName, IReadOnlyDictionary<string, object?> parameters, ToolExecutionResult result)
    {
        _toolInvocations.Add(new ToolInvocation(toolName, parameters, result));
    }
}

public sealed class EvaluationAssistantOrchestrator : IAssistantOrchestrator
{
    private readonly IAssistantOrchestrator _inner;
    private readonly IAiEvaluationTraceContext _traceContext;

    public EvaluationAssistantOrchestrator(IAssistantOrchestrator inner, IAiEvaluationTraceContext traceContext)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _traceContext = traceContext ?? throw new ArgumentNullException(nameof(traceContext));
    }

    public async Task<OrchestratorResult> ExecuteAsync(OrchestratorContext context, CancellationToken cancellationToken = default)
    {
        var result = await _inner.ExecuteAsync(context, cancellationToken);
        _traceContext.SetLastOrchestratorResult(result);
        return result;
    }
}

public sealed class EvaluationRetrievalService : IRetrievalService
{
    private readonly IRetrievalService _inner;
    private readonly IAiEvaluationTraceContext _traceContext;

    public EvaluationRetrievalService(IRetrievalService inner, IAiEvaluationTraceContext traceContext)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _traceContext = traceContext ?? throw new ArgumentNullException(nameof(traceContext));
    }

    public async Task<RetrievalResult> RetrieveAsync(string question, CancellationToken cancellationToken = default)
    {
        var result = await _inner.RetrieveAsync(question, cancellationToken);
        _traceContext.SetLastRetrievalResult(result);
        return result;
    }

    public async Task<RetrievalResult> RetrieveAsync(
        string question,
        Domain.DotNet.LocalizationOptions localizationOptions,
        CancellationToken cancellationToken = default)
    {
        var result = await _inner.RetrieveAsync(question, localizationOptions, cancellationToken);
        _traceContext.SetLastRetrievalResult(result);
        return result;
    }
}

public sealed class EvaluationToolExecutor : IToolExecutor
{
    private readonly IToolExecutor _inner;
    private readonly IAiEvaluationTraceContext _traceContext;

    public EvaluationToolExecutor(IToolExecutor inner, IAiEvaluationTraceContext traceContext)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _traceContext = traceContext ?? throw new ArgumentNullException(nameof(traceContext));
    }

    public IReadOnlyList<ToolDefinition> GetTools() => _inner.GetTools();

    public async Task<ToolExecutionResult> ExecuteAsync(
        string toolName,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var safeParameters = parameters ?? new Dictionary<string, object?>();
        if (_traceContext.TryGetScriptedToolResult(toolName, out var scripted) && scripted is not null)
        {
            _traceContext.CaptureToolInvocation(toolName, safeParameters, scripted);
            return scripted;
        }

        var result = await _inner.ExecuteAsync(toolName, parameters, cancellationToken);
        _traceContext.CaptureToolInvocation(toolName, safeParameters, result);
        return result;
    }
}

public sealed class EvaluationOllamaClient : IOllamaClient
{
    private readonly IOllamaClient _inner;
    private readonly IAiEvaluationTraceContext _traceContext;

    public EvaluationOllamaClient(IOllamaClient inner, IAiEvaluationTraceContext traceContext)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _traceContext = traceContext ?? throw new ArgumentNullException(nameof(traceContext));
    }

    public async Task<OllamaChatResult> ChatAsync(
        string? model,
        IReadOnlyList<ConversationMessage> messages,
        IReadOnlyList<ToolDefinition> tools,
        CancellationToken cancellationToken = default)
    {
        _traceContext.CaptureOllamaInvocation(messages, tools);
        if (_traceContext.TryDequeueScriptedLlmResponse(out var scripted))
            return scripted;

        return await _inner.ChatAsync(model, messages, tools, cancellationToken);
    }
}
