using Services.DotNet;

namespace Orchestration.DotNet;

public sealed class DefaultRoutingPolicy : IRoutingPolicy
{
    public string? DetermineDelegatedAgent(OrchestratorContext context, IReadOnlyList<string> registeredAgents)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(registeredAgents);

        // Preserve existing behavior: specialized delegation is not active by default.
        return null;
    }

    public AssistantRoute DetermineAssistantRoute(OrchestratorContext context, OllamaChatResult assistantResult, bool retrievalAvailable)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(assistantResult);

        if (assistantResult.HasToolCalls)
            return AssistantRoute.InvokeTools;

        return AssistantRoute.DirectAnswer;
    }
}
