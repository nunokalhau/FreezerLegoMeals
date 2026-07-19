namespace Orchestration.DotNet;

public sealed class ShoppingAgent : IAgent
{
    public string Name => "ShoppingAgent";

    public bool CanHandle(OrchestratorContext context) => false;

    public Task<OrchestratorResult> ExecuteAsync(OrchestratorContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(new OrchestratorResult("ShoppingAgent is not active yet.", Name, [], [], ["Assistant", "AssistantOrchestrator", Name], TimeSpan.Zero, ["Agent is not active."], context.MessagesToPersist));
}