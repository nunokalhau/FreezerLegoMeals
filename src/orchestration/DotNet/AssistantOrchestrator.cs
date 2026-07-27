using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Orchestration.DotNet;

public sealed class AssistantOrchestrator : IAssistantOrchestrator
{
    private static readonly ActivitySource ActivitySource = new("FreezerLegoMeals.AI");
    private readonly IReadOnlyList<IAgent> _agents;
    private readonly IRoutingPolicy _routingPolicy;
    private readonly ILogger<AssistantOrchestrator> _logger;

    public AssistantOrchestrator(
        IEnumerable<IAgent> agents,
        IRoutingPolicy routingPolicy,
        ILogger<AssistantOrchestrator> logger)
    {
        _agents = agents?.ToList() ?? throw new ArgumentNullException(nameof(agents));
        _routingPolicy = routingPolicy ?? throw new ArgumentNullException(nameof(routingPolicy));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<OrchestratorResult> ExecuteAsync(OrchestratorContext context, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("orchestration.select-agent", ActivityKind.Internal);
        activity?.SetTag("orchestration.correlation_id", context.CorrelationId);
        activity?.SetTag("orchestration.agent_count", _agents.Count);
        _logger.LogInformation("AssistantOrchestrator started for correlation {CorrelationId}", context.CorrelationId);

        var registeredAgentNames = _agents.Select(agent => agent.Name).ToList();
        var delegatedAgentName = _routingPolicy.DetermineDelegatedAgent(context, registeredAgentNames);
        IAgent? agent = null;
        if (!string.IsNullOrWhiteSpace(delegatedAgentName))
        {
            agent = _agents.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, delegatedAgentName, StringComparison.OrdinalIgnoreCase) && candidate.CanHandle(context));
            if (agent is not null)
            {
                _logger.LogInformation(
                    "Routing policy delegated correlation {CorrelationId} to agent {AgentName}",
                    context.CorrelationId,
                    agent.Name);
            }
            else
            {
                _logger.LogInformation(
                    "Routing policy requested agent {RequestedAgentName} but it was not available/eligible; falling back to default selection for correlation {CorrelationId}",
                    delegatedAgentName,
                    context.CorrelationId);
            }
        }

        agent ??= _agents.FirstOrDefault(candidate => candidate.CanHandle(context));
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