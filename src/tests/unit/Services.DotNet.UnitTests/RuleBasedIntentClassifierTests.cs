using Orchestration.DotNet;
using Xunit;

namespace Services.DotNet.UnitTests;

public sealed class RuleBasedIntentClassifierTests
{
    [Theory]
    [InlineData("What recipes do you have?")]
    [InlineData("What can I cook?")]
    [InlineData("Any chicken recipes?")]
    [InlineData("What dishes are available?")]
    [InlineData("Que receitas tens?")]
    [InlineData("Que pratos existem?")]
    [InlineData("Há alguma coisa com frango?")]
    public async Task ClassifyAsync_WithDefaultRules_UsesSemanticFallbackForDiscoveryLikePrompts(string message)
    {
        var classifier = new RuleBasedIntentClassifier();

        var result = await classifier.ClassifyAsync(message);

        Assert.Equal(IntentType.RecipeDiscovery, result.Intent);
        Assert.Equal("semantic-fallback", result.MatchedRule);
        Assert.Equal(0.45, result.Confidence);
    }

    [Theory]
    [InlineData("Show me meals")]
    [InlineData("Mostra-me refeições")]
    public async Task ClassifyAsync_WithDefaultRules_UsesGeneralFallbackForStatementsWithoutQuestionMark(string message)
    {
        var classifier = new RuleBasedIntentClassifier();

        var result = await classifier.ClassifyAsync(message);

        Assert.Equal(IntentType.GeneralConversation, result.Intent);
        Assert.Equal("semantic-fallback", result.MatchedRule);
        Assert.Equal(0.45, result.Confidence);
    }

    [Fact]
    public async Task ClassifyAsync_WithLongStatement_UsesSemanticFallbackGeneralConversation()
    {
        var classifier = new RuleBasedIntentClassifier();

        var result = await classifier.ClassifyAsync("I am sharing context and do not need suggestions right now");

        Assert.Equal(IntentType.GeneralConversation, result.Intent);
        Assert.Equal("semantic-fallback", result.MatchedRule);
    }

    [Fact]
    public async Task ClassifyAsync_WhenMessageEmpty_ReturnsGeneralConversationFallback()
    {
        var classifier = new RuleBasedIntentClassifier();

        var result = await classifier.ClassifyAsync("   ");

        Assert.Equal(IntentType.GeneralConversation, result.Intent);
        Assert.Equal("empty", result.MatchedRule);
        Assert.Equal(0.5, result.Confidence);
    }

    [Fact]
    public async Task ClassifyAsync_RespectsPreferredLanguage()
    {
        var classifier = new RuleBasedIntentClassifier();

        var result = await classifier.ClassifyAsync("What recipes do you have?", preferredLanguage: "pt-BR");

        Assert.Equal("pt-BR", result.Language);
    }

    [Fact]
    public async Task ClassifyAsync_DetectsLanguageWhenPreferenceMissing()
    {
        var classifier = new RuleBasedIntentClassifier();

        var result = await classifier.ClassifyAsync("Que receitas tens com frango?");

        Assert.Equal("en", result.Language);
    }

    [Fact]
    public async Task ClassifyAsync_WithCustomRuleSet_IsComposable()
    {
        var classifier = new RuleBasedIntentClassifier([
            new FixedScoreRule("custom-high", IntentType.MealPlanning, 0.9),
            new FixedScoreRule("custom-low", IntentType.RecipeDiscovery, 0.5)
        ]);

        var result = await classifier.ClassifyAsync("anything");

        Assert.Equal(IntentType.MealPlanning, result.Intent);
        Assert.Equal("custom-high", result.MatchedRule);
        Assert.Equal(0.9, result.Confidence);
    }

    private sealed class FixedScoreRule : IIntentDetectionRule
    {
        private readonly double _score;

        public FixedScoreRule(string ruleName, IntentType intent, double score)
        {
            RuleName = ruleName;
            Intent = intent;
            _score = score;
        }

        public string RuleName { get; }

        public IntentType Intent { get; }

        public double Score(string normalizedMessage) => _score;
    }
}
