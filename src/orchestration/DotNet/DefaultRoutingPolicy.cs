using Services.DotNet;

namespace Orchestration.DotNet;

public sealed class DefaultRoutingPolicy : IRoutingPolicy
{
    private static readonly string[] RepositoryKnowledgeTerms =
    [
        "recipe",
        "recipes",
        "receita",
        "receitas",
        "receta",
        "recetas",
        "recette",
        "recettes",
        "rezept",
        "rezepte",
        "meal",
        "meals",
        "refeicao",
        "refeicoes",
        "comida",
        "comidas",
        "repas",
        "mahlzeit",
        "mahlzeiten",
        "cook",
        "cooking",
        "cozinhar",
        "cozinha",
        "cocinar",
        "cuisine",
        "kochen",
        "dinner",
        "lunch",
        "jantar",
        "almoco",
        "cena",
        "dejeuner",
        "mittagessen",
        "freezer",
        "congelador",
        "gefrierschrank",
        "congelateur",
        "ingredient",
        "ingredients",
        "ingrediente",
        "ingredientes",
        "prep",
        "preparation",
        "preparar",
        "preparo",
        "what can i",
        "what should i",
        "recommend",
        "que receitas",
        "que comida",
        "que puis-je",
        "welche rezepte"
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
