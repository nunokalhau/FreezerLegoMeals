namespace Orchestration.DotNet;

public interface IAgent
{
    string Name { get; }

    bool CanHandle(OrchestratorContext context);

    Task<OrchestratorResult> ExecuteAsync(OrchestratorContext context, CancellationToken cancellationToken = default);
}