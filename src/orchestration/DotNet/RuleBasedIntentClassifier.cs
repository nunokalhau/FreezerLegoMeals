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

public sealed class RuleBasedIntentClassifier : IIntentClassifier
{
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

        return Task.FromResult(BuildSemanticFallback(message, language));
    }

    private static IReadOnlyList<IIntentDetectionRule> CreateDefaultRules()
    {
        return [];
    }

    private static IntentClassificationResult BuildSemanticFallback(string message, string language)
    {
        var normalized = message.Trim();
        var hasQuestionMark = normalized.Contains('?');

        if (hasQuestionMark)
        {
            return new IntentClassificationResult(IntentType.RecipeDiscovery, 0.45, "semantic-fallback", language);
        }

        return new IntentClassificationResult(IntentType.GeneralConversation, 0.45, "semantic-fallback", language);
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

        return "en";
    }
}