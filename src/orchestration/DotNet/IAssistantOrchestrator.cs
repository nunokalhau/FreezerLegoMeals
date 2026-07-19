namespace Orchestration.DotNet;

public interface IAssistantOrchestrator
{
    Task<OrchestratorResult> ExecuteAsync(OrchestratorContext context, CancellationToken cancellationToken = default);
}