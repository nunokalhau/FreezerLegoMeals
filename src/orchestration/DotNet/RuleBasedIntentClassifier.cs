namespace Orchestration.DotNet;

public interface IIntentDetectionRule
{
    string RuleName { get; }

    IntentType Intent { get; }

    double Score(string normalizedMessage);
}

public abstract class QuestionPatternRule : IIntentDetectionRule
{
    private readonly IReadOnlyList<string> _patterns;
    private readonly double _matchScore;

    protected QuestionPatternRule(string ruleName, IntentType intent, IReadOnlyList<string> patterns, double matchScore = 0.7)
    {
        RuleName = ruleName;
        Intent = intent;
        _patterns = patterns;
        _matchScore = matchScore;
    }

    public string RuleName { get; }

    public IntentType Intent { get; }

    public virtual double Score(string normalizedMessage)
    {
        return _patterns.Any(normalizedMessage.Contains) ? _matchScore : 0;
    }
}

public sealed class RecipeDiscoveryRule : QuestionPatternRule
{
    private static readonly string[] Patterns =
    [
        "recipe", "recipes", "receita", "receitas", "receta", "recetas", "recette", "recettes", "rezept", "rezepte",
        "what do you have", "what recipes", "que receitas", "quais receitas", "que tens", "which recipes", "list"
    ];

    public RecipeDiscoveryRule() : base("recipe-discovery", IntentType.RecipeDiscovery, Patterns)
    {
    }
}

public sealed class RecipeDetailsRule : QuestionPatternRule
{
    private static readonly string[] Patterns =
    [
        "how to make", "instructions", "steps", "preparation", "prep", "cook time", "tempo"
    ];

    public RecipeDetailsRule() : base("recipe-details", IntentType.RecipeDetails, Patterns)
    {
    }
}

public sealed class IngredientSearchRule : QuestionPatternRule
{
    private static readonly string[] Patterns =
    [
        "ingredient", "ingredients", "with chicken", "com frango", "contains", "have"
    ];

    public IngredientSearchRule() : base("ingredient-search", IntentType.IngredientSearch, Patterns)
    {
    }
}

public sealed class MealPlanningRule : QuestionPatternRule
{
    private static readonly string[] Patterns =
    [
        "meal plan", "weekly", "week", "plan", "schedule", "batch cooking"
    ];

    public MealPlanningRule() : base("meal-planning", IntentType.MealPlanning, Patterns)
    {
    }
}

public sealed class CookingQuestionRule : QuestionPatternRule
{
    private static readonly string[] Patterns =
    [
        "how long", "temperature", "substitute", "replace", "freeze", "reheat"
    ];

    public CookingQuestionRule() : base("cooking-question", IntentType.CookingQuestion, Patterns)
    {
    }
}

public sealed class RuleBasedIntentClassifier : IIntentClassifier
{
    private static readonly string[] PortugueseMarkers = [" que ", " receitas", " frango", " com ", " cozinhar", " refeicao", " jantar"];
    private static readonly string[] SpanishMarkers = [" que ", " recetas", " pollo", " cocinar", " comida", " cena", " ingredientes"];
    private static readonly string[] FrenchMarkers = [" quelle", " recettes", " poulet", " cuisiner", " repas", " diner", " ingredients"];
    private static readonly string[] GermanMarkers = [" welche", " rezepte", " huhn", " kochen", " mahlzeit", " zutaten"];
    private readonly IReadOnlyList<IIntentDetectionRule> _rules;

    public RuleBasedIntentClassifier()
        : this(CreateDefaultRules())
    {
    }

    public RuleBasedIntentClassifier(IEnumerable<IIntentDetectionRule> rules)
    {
        _rules = rules?.ToList() ?? throw new ArgumentNullException(nameof(rules));
    }

    public Task<IntentClassificationResult> ClassifyAsync(
        string message,
        string? preferredLanguage = null,
        CancellationToken cancellationToken = default)
    {
        var language = ResolveLanguage(preferredLanguage, message);

        if (string.IsNullOrWhiteSpace(message))
        {
            return Task.FromResult(new IntentClassificationResult(IntentType.GeneralConversation, 0.5, "empty", language));
        }

        var normalized = message.ToLowerInvariant();
        IIntentDetectionRule? bestRule = null;
        var bestScore = 0d;

        foreach (var rule in _rules)
        {
            var score = rule.Score(normalized);
            if (score > bestScore)
            {
                bestScore = score;
                bestRule = rule;
            }
        }

        if (bestRule is not null)
        {
            return Task.FromResult(new IntentClassificationResult(bestRule.Intent, bestScore, bestRule.RuleName, language));
        }

        return Task.FromResult(new IntentClassificationResult(IntentType.GeneralConversation, 0.6, "fallback", language));
    }

    private static IReadOnlyList<IIntentDetectionRule> CreateDefaultRules()
    {
        return
        [
            new RecipeDiscoveryRule(),
            new RecipeDetailsRule(),
            new IngredientSearchRule(),
            new MealPlanningRule(),
            new CookingQuestionRule()
        ];
    }

    private static bool ContainsAny(string normalized, IReadOnlyList<string> terms)
    {
        return terms.Any(normalized.Contains);
    }

    private static string ResolveLanguage(string? preferredLanguage, string message)
    {
        if (!string.IsNullOrWhiteSpace(preferredLanguage))
        {
            return preferredLanguage.Trim();
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return "en";
        }

        var normalized = $" {message.ToLowerInvariant()} ";

        if (ContainsAny(normalized, PortugueseMarkers))
        {
            return "pt";
        }

        if (ContainsAny(normalized, SpanishMarkers))
        {
            return "es";
        }

        if (ContainsAny(normalized, FrenchMarkers))
        {
            return "fr";
        }

        if (ContainsAny(normalized, GermanMarkers))
        {
            return "de";
        }

        return "en";
    }
}