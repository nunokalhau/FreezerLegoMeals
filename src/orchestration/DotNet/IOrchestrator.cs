namespace Orchestration.DotNet;

public interface IOrchestrator
{
    Task<OrchestratorResult> ExecuteAsync(OrchestratorContext context, CancellationToken cancellationToken = default);
}