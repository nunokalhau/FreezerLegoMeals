using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Orchestration.DotNet;

public sealed class AssistantOrchestrator : IAssistantOrchestrator
{
    private static readonly ActivitySource ActivitySource = new("FreezerLegoMeals.AI");
    private readonly IReadOnlyList<IAgent> _agents;
    private readonly ILogger<AssistantOrchestrator> _logger;

    public AssistantOrchestrator(IEnumerable<IAgent> agents, ILogger<AssistantOrchestrator> logger)
    {
        _agents = agents?.ToList() ?? throw new ArgumentNullException(nameof(agents));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<OrchestratorResult> ExecuteAsync(OrchestratorContext context, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("orchestration.select-agent", ActivityKind.Internal);
        activity?.SetTag("orchestration.correlation_id", context.CorrelationId);
        activity?.SetTag("orchestration.agent_count", _agents.Count);
        _logger.LogInformation("AssistantOrchestrator started for correlation {CorrelationId}", context.CorrelationId);
        var agent = _agents.FirstOrDefault(candidate => candidate.CanHandle(context));
        if (agent is null)
        {
            _logger.LogWarning("No orchestration agent could handle correlation {CorrelationId}", context.CorrelationId);
            var error = "No assistant agent is available to handle that request.";
            activity?.SetTag("orchestration.selected_agent", "none");
            activity?.SetTag("orchestration.selection_result", "no-agent");
            return new OrchestratorResult(error, "none", [], [], ["Assistant", "AssistantOrchestrator", "NoAgent"], TimeSpan.Zero, [error], context.MessagesToPersist);
        }

        activity?.SetTag("orchestration.selected_agent", agent.Name);
        activity?.SetTag("orchestration.selection_result", "selected");
        _logger.LogInformation("AssistantOrchestrator selected {AgentName} for correlation {CorrelationId}", agent.Name, context.CorrelationId);
        return await agent.ExecuteAsync(context, cancellationToken);
    }
}