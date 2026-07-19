using Microsoft.Extensions.Logging;

namespace Orchestration.DotNet;

public sealed class AssistantOrchestrator : IAssistantOrchestrator
{
    private readonly IReadOnlyList<IAgent> _agents;
    private readonly ILogger<AssistantOrchestrator> _logger;

    public AssistantOrchestrator(IEnumerable<IAgent> agents, ILogger<AssistantOrchestrator> logger)
    {
        _agents = agents?.ToList() ?? throw new ArgumentNullException(nameof(agents));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<OrchestratorResult> ExecuteAsync(OrchestratorContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("AssistantOrchestrator started for correlation {CorrelationId}", context.CorrelationId);
        var agent = _agents.FirstOrDefault(candidate => candidate.CanHandle(context));
        if (agent is null)
        {
            _logger.LogWarning("No orchestration agent could handle correlation {CorrelationId}", context.CorrelationId);
            var error = "No assistant agent is available to handle that request.";
            return new OrchestratorResult(error, "none", [], [], ["Assistant", "AssistantOrchestrator", "NoAgent"], TimeSpan.Zero, [error], context.MessagesToPersist);
        }

        _logger.LogInformation("AssistantOrchestrator selected {AgentName} for correlation {CorrelationId}", agent.Name, context.CorrelationId);
        return await agent.ExecuteAsync(context, cancellationToken);
    }
}