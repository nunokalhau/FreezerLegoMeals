namespace Orchestration.DotNet;

public sealed class NutritionAgent : IAgent
{
    public string Name => "NutritionAgent";

    public bool CanHandle(OrchestratorContext context) => false;

    public Task<OrchestratorResult> ExecuteAsync(OrchestratorContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(new OrchestratorResult("NutritionAgent is not active yet.", Name, [], [], ["Assistant", "Orchestrator", Name], TimeSpan.Zero, ["Agent is not active."], context.MessagesToPersist));
}