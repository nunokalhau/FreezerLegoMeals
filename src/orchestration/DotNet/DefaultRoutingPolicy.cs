using Services.DotNet;

namespace Orchestration.DotNet;

public sealed class DefaultRoutingPolicy : IRoutingPolicy
{
    private static readonly string[] RepositoryKnowledgeTerms =
    [
        "recipe",
        "recipes",
        "meal",
        "meals",
        "cook",
        "cooking",
        "dinner",
        "lunch",
        "freezer",
        "ingredient",
        "ingredients",
        "prep",
        "preparation",
        "what can i",
        "what should i",
        "recommend"
    ];

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

        if (retrievalAvailable && RequiresRepositoryKnowledge(context.UserRequest))
            return AssistantRoute.UseRag;

        return AssistantRoute.DirectAnswer;
    }

    private static bool RequiresRepositoryKnowledge(string message)
    {
        var normalized = message.ToLowerInvariant();
        return RepositoryKnowledgeTerms.Any(normalized.Contains);
    }
}
