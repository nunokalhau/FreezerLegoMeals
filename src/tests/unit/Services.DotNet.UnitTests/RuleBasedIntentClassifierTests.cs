using Orchestration.DotNet;
using Xunit;

namespace Services.DotNet.UnitTests;

public sealed class RuleBasedIntentClassifierTests
{
    [Theory]
    [InlineData("What recipes do you have with chicken?", IntentType.RecipeDiscovery, "recipe-discovery")]
    [InlineData("How to make spicy chicken?", IntentType.RecipeDetails, "recipe-details")]
    [InlineData("Which meals contain garlic ingredient?", IntentType.IngredientSearch, "ingredient-search")]
    [InlineData("Can you build me a weekly meal plan?", IntentType.MealPlanning, "meal-planning")]
    [InlineData("How long should I reheat this?", IntentType.CookingQuestion, "cooking-question")]
    public async Task ClassifyAsync_WithDefaultRules_ReturnsExpectedIntent(string message, IntentType expectedIntent, string expectedRule)
    {
        var classifier = new RuleBasedIntentClassifier();

        var result = await classifier.ClassifyAsync(message);

        Assert.Equal(expectedIntent, result.Intent);
        Assert.Equal(expectedRule, result.MatchedRule);
        Assert.Equal(0.7, result.Confidence);
    }

    [Fact]
    public async Task ClassifyAsync_WhenMultipleRulesMatch_PreservesRuleOrder()
    {
        var classifier = new RuleBasedIntentClassifier();

        var result = await classifier.ClassifyAsync("What recipes do you have?");

        Assert.Equal(IntentType.RecipeDiscovery, result.Intent);
        Assert.Equal("recipe-discovery", result.MatchedRule);
    }

    [Fact]
    public async Task ClassifyAsync_WhenNoRuleMatches_ReturnsGeneralConversationFallback()
    {
        var classifier = new RuleBasedIntentClassifier();

        var result = await classifier.ClassifyAsync("Hello there");

        Assert.Equal(IntentType.GeneralConversation, result.Intent);
        Assert.Equal("fallback", result.MatchedRule);
        Assert.Equal(0.6, result.Confidence);
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

        Assert.Equal("pt", result.Language);
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
