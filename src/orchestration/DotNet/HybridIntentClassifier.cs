using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Services.DotNet;

namespace Orchestration.DotNet;

public sealed class HybridIntentClassifier : IIntentClassifier
{
    private readonly RuleBasedIntentClassifier _ruleBasedClassifier;
    private readonly IOllamaClient _ollamaClient;
    private readonly HybridIntentClassifierOptions _options;
    private readonly ILogger<HybridIntentClassifier> _logger;

    public HybridIntentClassifier(
        RuleBasedIntentClassifier ruleBasedClassifier,
        IOllamaClient ollamaClient,
        IOptions<HybridIntentClassifierOptions> options,
        ILogger<HybridIntentClassifier> logger)
    {
        _ruleBasedClassifier = ruleBasedClassifier ?? throw new ArgumentNullException(nameof(ruleBasedClassifier));
        _ollamaClient = ollamaClient ?? throw new ArgumentNullException(nameof(ollamaClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IntentClassificationResult> ClassifyAsync(
        string message,
        string? preferredLanguage = null,
        CancellationToken cancellationToken = default)
    {
        var ruleResult = await _ruleBasedClassifier.ClassifyAsync(message, preferredLanguage, cancellationToken);

        if (ruleResult.Confidence >= _options.LowConfidenceThreshold)
        {
            return ruleResult;
        }

        try
        {
            var now = DateTimeOffset.UtcNow;
            var response = await _ollamaClient.ChatAsync(
                _options.Model,
                [
                    new ConversationMessage(ConversationRole.System, BuildSystemPrompt(), now),
                    new ConversationMessage(ConversationRole.User, BuildUserPrompt(message, preferredLanguage, ruleResult), now)
                ],
                [],
                cancellationToken);

            if (TryParseLlmResult(response.Content, ruleResult, out var llmResult) && llmResult is not null)
            {
                return llmResult;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Hybrid intent classification LLM fallback failed; using rule-based result.");
        }

        return ruleResult;
    }

    private static string BuildSystemPrompt()
    {
        return "You classify user intent for a meal-planning assistant. Respond with strict JSON only.";
    }

    private static string BuildUserPrompt(string message, string? preferredLanguage, IntentClassificationResult ruleResult)
    {
        return $"Classify the user message into exactly one intent from this list:\n"
            + "- RecipeDiscovery\n"
            + "- RecipeDetails\n"
            + "- IngredientSearch\n"
            + "- MealPlanning\n"
            + "- CookingQuestion\n"
            + "- GeneralConversation\n\n"
            + "Return only JSON with shape:\n"
            + "{\"intent\":\"<intent>\",\"confidence\":<0..1>,\"language\":\"<language-code>\"}\n\n"
            + "Hints:\n"
            + $"- preferredLanguage: {preferredLanguage ?? "(none)"}\n"
            + $"- ruleBasedIntent: {ruleResult.Intent}\n"
            + $"- ruleBasedConfidence: {ruleResult.Confidence}\n"
            + $"- userMessage: {message}";
    }

    private static bool TryParseLlmResult(string content, IntentClassificationResult ruleResult, out IntentClassificationResult? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        var json = ExtractJsonObject(content);
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("intent", out var intentElement))
            {
                return false;
            }

            var intentText = intentElement.GetString();
            if (string.IsNullOrWhiteSpace(intentText) || !Enum.TryParse<IntentType>(intentText, true, out var intent))
            {
                return false;
            }

            var confidence = root.TryGetProperty("confidence", out var confidenceElement) && confidenceElement.TryGetDouble(out var parsedConfidence)
                ? Math.Clamp(parsedConfidence, 0, 1)
                : ruleResult.Confidence;

            var language = root.TryGetProperty("language", out var languageElement)
                ? languageElement.GetString()
                : ruleResult.Language;

            result = new IntentClassificationResult(intent, confidence, "hybrid-llm", string.IsNullOrWhiteSpace(language) ? ruleResult.Language : language);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? ExtractJsonObject(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return null;
        }

        return text[start..(end + 1)];
    }
}