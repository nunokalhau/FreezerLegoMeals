using Services.DotNet;

namespace Orchestration.DotNet;

public enum AssistantRoute
{
    DirectAnswer,
    UseRag,
    InvokeTools
}

public interface IRoutingPolicy
{
    string? DetermineDelegatedAgent(OrchestratorContext context, IReadOnlyList<string> registeredAgents);

    AssistantRoute DetermineAssistantRoute(OrchestratorContext context, OllamaChatResult assistantResult, bool retrievalAvailable);
}
