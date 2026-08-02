using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Domain.DotNet;
using Microsoft.Extensions.Logging;
using RAG.DotNet;
using Services.DotNet;

namespace Orchestration.DotNet;

public sealed class MealPlanningAgent : IAgent
{
    private static readonly ActivitySource ActivitySource = new("FreezerLegoMeals.AI");
    private static readonly Regex SourceLineRegex = new(
        @"^\s*-\s*(?<id>[A-Za-z0-9][\w\-./]*)\s*:\s*(?<title>.+?)\s*$",
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.CultureInvariant);
    private static readonly string[] ConversationalReferencePhrases =
    [
        "it",
        "that recipe",
        "the recipe you suggested",
        "recipe you suggested",
        "that one",
        "this meal",
        "aquela receita",
        "essa receita",
        "a receita que sugeriste",
        "receita que sugeriste",
        "o prato anterior"
    ];
    private const string InfrastructureFailureResponse = "The assistant is temporarily unavailable. Please try again.";
    private readonly IOllamaClient _ollamaClient;
    private readonly IToolExecutor _toolExecutor;
    private readonly IRoutingPolicy _routingPolicy;
    private readonly ILogger<MealPlanningAgent> _logger;
    private readonly IIntentClassifier _intentClassifier;
    private readonly IRetrievalService? _retrievalService;
    private readonly IPromptBuilder? _promptBuilder;
    private readonly IAnswerGroundingService? _answerGroundingService;

    public MealPlanningAgent(
        IOllamaClient ollamaClient,
        IToolExecutor toolExecutor,
        IRoutingPolicy routingPolicy,
        ILogger<MealPlanningAgent> logger,
        IRetrievalService? retrievalService = null,
        IPromptBuilder? promptBuilder = null,
        IAnswerGroundingService? answerGroundingService = null,
        IIntentClassifier? intentClassifier = null)
    {
        _ollamaClient = ollamaClient ?? throw new ArgumentNullException(nameof(ollamaClient));
        _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
        _routingPolicy = routingPolicy ?? throw new ArgumentNullException(nameof(routingPolicy));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _retrievalService = retrievalService;
        _promptBuilder = promptBuilder;
        _answerGroundingService = answerGroundingService;
        _intentClassifier = intentClassifier ?? new RuleBasedIntentClassifier();
    }

    public string Name => "MealPlanningAgent";

    public bool CanHandle(OrchestratorContext context) => true;

    public async Task<OrchestratorResult> ExecuteAsync(OrchestratorContext context, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("orchestration.agent.execute", ActivityKind.Internal);
        activity?.SetTag("agent.name", Name);
        activity?.SetTag("assistant.correlation_id", context.CorrelationId);
        activity?.SetTag("assistant.user_request_length", context.UserRequest.Length);
        var intent = await _intentClassifier.ClassifyAsync(
            context.UserRequest,
            context.LocalizationOptions.PreferredLanguage,
            cancellationToken);
        activity?.SetTag("assistant.intent", intent.Intent.ToString());
        activity?.SetTag("assistant.intent_confidence", intent.Confidence);
        activity?.SetTag("assistant.intent_language", intent.Language ?? string.Empty);
        _logger.LogDebug(
            "{AgentName} intent classification correlation={CorrelationId} intent={Intent} confidence={Confidence} matchedRule={MatchedRule} language={Language}",
            Name,
            context.CorrelationId,
            intent.Intent,
            intent.Confidence,
            intent.MatchedRule ?? "none",
            intent.Language ?? "unknown");

        var startedAt = Stopwatch.StartNew();
        var messages = context.Messages.ToList();
        var messagesToPersist = context.MessagesToPersist.ToList();
        var tools = _toolExecutor.GetTools();
        var totalToolCalls = 0;
        var executedTools = new List<string>();
        var retrievedRecipes = new List<RetrievedRecipeInfo>();
        var errors = new List<string>();
        var steps = new List<string> { "Assistant", "AssistantOrchestrator", Name };

        while (true)
        {
            if (HasExceededExecutionLimits(context.AssistantOptions, messages, startedAt, out var limitError))
            {
                errors.Add(limitError);
                return BuildResult(context, limitError, messagesToPersist, executedTools, retrievedRecipes, steps, errors, startedAt);
            }

            steps.Add("Ollama");
            _logger.LogInformation("{AgentName} invoking Ollama for correlation {CorrelationId}", Name, context.CorrelationId);
            var assistantResult = await _ollamaClient.ChatAsync(null, messages, tools, cancellationToken);

            var retrievalAvailable = _retrievalService is not null && _promptBuilder is not null;
            var route = _routingPolicy.DetermineAssistantRoute(context, assistantResult, retrievalAvailable);
            if (route != AssistantRoute.InvokeTools)
            {
                route = ShouldUseRetrieval(intent, retrievalAvailable)
                    ? AssistantRoute.UseRag
                    : AssistantRoute.DirectAnswer;
            }
            _logger.LogInformation(
                "{AgentName} routing decision correlation={CorrelationId} route={Route} hasToolCalls={HasToolCalls} retrievalAvailable={RetrievalAvailable}",
                Name,
                context.CorrelationId,
                route,
                assistantResult.HasToolCalls,
                retrievalAvailable);

            if (route != AssistantRoute.InvokeTools)
            {
                var content = assistantResult.Content;
                if (route == AssistantRoute.UseRag && _retrievalService is not null && _promptBuilder is not null)
                {
                    steps.Add("Semantic Search");
                    steps.Add("Retrieval");
                    steps.Add("Prompt Builder");
                    steps.Add("RAG");
                    var ragResult = await AnswerWithRetrievalAsync(context, intent, activity, cancellationToken);
                    content = ragResult.Response;
                    retrievedRecipes.AddRange(ragResult.RetrievedRecipes);
                    _logger.LogInformation(
                        "{AgentName} RAG decision correlation={CorrelationId} retrievedRecipes={RetrievedRecipeCount}",
                        Name,
                        context.CorrelationId,
                        ragResult.RetrievedRecipes.Count);
                }

                steps.Add("Answer");
                var finalMessage = new ConversationMessage(ConversationRole.Assistant, content, DateTimeOffset.UtcNow);
                messagesToPersist.Add(finalMessage);
                _logger.LogInformation("{AgentName} completed with {TotalToolCalls} tool calls", Name, totalToolCalls);
                activity?.SetTag("assistant.tool_call_count", totalToolCalls);
                activity?.SetTag("assistant.retrieved_recipe_count", retrievedRecipes.Count);
                activity?.SetTag("assistant.error_count", errors.Count);
                return BuildResult(context, content, messagesToPersist, executedTools, retrievedRecipes, steps, errors, startedAt);
            }

            if (!string.IsNullOrWhiteSpace(assistantResult.Content))
            {
                var assistantMessage = new ConversationMessage(ConversationRole.Assistant, assistantResult.Content, DateTimeOffset.UtcNow);
                messages.Add(assistantMessage);
                messagesToPersist.Add(assistantMessage);
            }

            foreach (var toolCall in assistantResult.ToolCalls)
            {
                if (totalToolCalls >= context.AssistantOptions.MaximumToolCallsPerRequest)
                {
                    var error = $"The request could not be completed because it exceeded the maximum tool call limit of {context.AssistantOptions.MaximumToolCallsPerRequest}.";
                    errors.Add(error);
                    return BuildResult(context, error, messagesToPersist, executedTools, retrievedRecipes, steps, errors, startedAt);
                }

                var toolStartedAt = Stopwatch.StartNew();
                totalToolCalls++;
                steps.Add("ToolExecutor");
                executedTools.Add(toolCall.Name);
                _logger.LogInformation("{AgentName} requested tool {ToolName} with arguments {ToolArguments}", Name, toolCall.Name, JsonSerializer.Serialize(toolCall.Arguments));

                ToolExecutionResult toolResult;
                try
                {
                    toolResult = await _toolExecutor.ExecuteAsync(toolCall.Name, toolCall.Arguments, cancellationToken);
                }
                catch (Exception ex)
                {
                    toolResult = new ToolExecutionResult
                    {
                        Success = false,
                        Tool = toolCall.Name,
                        Error = ex.Message
                    };
                    errors.Add(ex.Message);
                }
                finally
                {
                    toolStartedAt.Stop();
                }

                _logger.LogInformation(
                    "{AgentName} tool {ToolName} finished in {ExecutionDurationMs}ms with success={ToolSuccess}",
                    Name,
                    toolResult.Tool,
                    toolStartedAt.ElapsedMilliseconds,
                    toolResult.Success);

                var toolMessage = new ConversationMessage(ConversationRole.Tool, JsonSerializer.Serialize(toolResult), DateTimeOffset.UtcNow);
                messages.Add(toolMessage);
                messagesToPersist.Add(toolMessage);
            }
        }
    }

    private static bool HasExceededExecutionLimits(AssistantOptions options, IReadOnlyList<ConversationMessage> messages, Stopwatch startedAt, out string error)
    {
        if (options.MaximumConversationSize > 0 && messages.Count > options.MaximumConversationSize)
        {
            error = $"The request could not be completed because the conversation exceeded the maximum size of {options.MaximumConversationSize} messages.";
            return true;
        }

        if (options.MaximumExecutionTime > TimeSpan.Zero && startedAt.Elapsed > options.MaximumExecutionTime)
        {
            error = $"The request could not be completed because it exceeded the maximum execution time of {options.MaximumExecutionTime}.";
            return true;
        }

        error = string.Empty;
        return false;
    }

    private async Task<(string Response, IReadOnlyList<RetrievedRecipeInfo> RetrievedRecipes)> AnswerWithRetrievalAsync(
        OrchestratorContext context,
        IntentClassificationResult intent,
        Activity? activity,
        CancellationToken cancellationToken)
    {
        var retrievalQuery = ResolveRetrievalQuery(context.UserRequest, context.Messages);
        activity?.SetTag("retrieval.original_user_query", context.UserRequest);
        activity?.SetTag("retrieval.effective_query", retrievalQuery);

        var retrievalRequest = new RetrievalRequestContext(
            retrievalQuery,
            new RetrievalIntentClassification(
                intent.Intent.ToString(),
                intent.Confidence,
                intent.MatchedRule,
                intent.Language),
            context.LocalizationOptions,
            context.LocalizationOptions.StrictMode);
        var retrieval = await _retrievalService!.RetrieveAsync(retrievalRequest, cancellationToken);
        activity?.SetTag("retrieval.profile_id", retrieval.Profile?.ProfileId ?? string.Empty);
        activity?.SetTag("retrieval.profile_family", retrieval.Profile?.ProfileFamily.ToString() ?? string.Empty);
        activity?.SetTag("retrieval.normalization_version", retrieval.NormalizationVersion);
        var retrievedRecipes = retrieval.Sources
            .Select(source => new RetrievedRecipeInfo(source.RecipeId, source.Title, source.RetrievalScore))
            .ToList();

        var prompt = _promptBuilder!.Build(
            context.UserRequest,
            retrieval.Recipes,
            intent.Intent.ToString(),
            context.LocalizationOptions,
            ResolveRequestedLanguage(context.LanguageContext));
        _logger.LogInformation(
            "{AgentName} RAG model context correlation={CorrelationId} userQuestion={UserQuestion} retrievedCount={RetrievedCount} recipes={RecipeSummaries} prompt={Prompt}",
            Name,
            context.CorrelationId,
            context.UserRequest,
            retrieval.Recipes.Count,
            string.Join(" | ", retrieval.Recipes.Select(recipe => $"{recipe.RecipeId}:{recipe.Title}")),
            prompt);
        var now = DateTimeOffset.UtcNow;
        var response = await _ollamaClient.ChatAsync(null, [
            new ConversationMessage(ConversationRole.System, context.AssistantOptions.SystemPrompt, now),
            new ConversationMessage(ConversationRole.User, prompt, now)
        ], [], cancellationToken);
        var content = string.IsNullOrWhiteSpace(response.Content)
            ? string.Empty
            : response.Content.Trim();

        var grounding = _answerGroundingService is null
            ? new AnswerGroundingResult(true, 0)
            : await _answerGroundingService.ValidateAsync(content, retrieval.Recipes, cancellationToken);

        _logger.LogInformation(
            "{AgentName} answer grounding correlation={CorrelationId} grounded={Grounded} unsupportedClaimsCount={UnsupportedClaimsCount}",
            Name,
            context.CorrelationId,
            grounding.Grounded,
            grounding.UnsupportedClaimsCount);
        activity?.SetTag("grounding.grounded", grounding.Grounded);
        activity?.SetTag("grounding.unsupported_claims_count", grounding.UnsupportedClaimsCount);

        if (!grounding.Grounded || string.IsNullOrWhiteSpace(content))
        {
            _logger.LogInformation(
                "{AgentName} retrying RAG generation after grounding failure correlation={CorrelationId}",
                Name,
                context.CorrelationId);

            var retryNow = DateTimeOffset.UtcNow;
            var retryResponse = await _ollamaClient.ChatAsync(null, [
                new ConversationMessage(ConversationRole.System, context.AssistantOptions.SystemPrompt, retryNow),
                new ConversationMessage(ConversationRole.User, prompt, retryNow)
            ], [], cancellationToken);

            var retryContent = string.IsNullOrWhiteSpace(retryResponse.Content)
                ? string.Empty
                : retryResponse.Content.Trim();

            var retryGrounding = _answerGroundingService is null
                ? new AnswerGroundingResult(true, 0)
                : await _answerGroundingService.ValidateAsync(retryContent, retrieval.Recipes, cancellationToken);

            _logger.LogInformation(
                "{AgentName} retry answer grounding correlation={CorrelationId} grounded={Grounded} unsupportedClaimsCount={UnsupportedClaimsCount}",
                Name,
                context.CorrelationId,
                retryGrounding.Grounded,
                retryGrounding.UnsupportedClaimsCount);
            activity?.SetTag("grounding.retry_grounded", retryGrounding.Grounded);
            activity?.SetTag("grounding.retry_unsupported_claims_count", retryGrounding.UnsupportedClaimsCount);

            if (string.IsNullOrWhiteSpace(retryContent))
            {
                _logger.LogError(
                    "{AgentName} RAG generation returned empty response after retry correlation={CorrelationId}",
                    Name,
                    context.CorrelationId);
                return ($"{InfrastructureFailureResponse}\n\n{FormatSources(retrieval.Sources)}", retrievedRecipes);
            }

            return ($"{retryContent}\n\n{FormatSources(retrieval.Sources)}", retrievedRecipes);
        }

        _logger.LogInformation(
            "{AgentName} retrieval-backed answer correlation={CorrelationId} sourceCount={SourceCount}",
            Name,
            context.CorrelationId,
            retrieval.Sources.Count);

        return ($"{content}\n\n{FormatSources(retrieval.Sources)}", retrievedRecipes);
    }

    private string ResolveRetrievalQuery(string userRequest, IReadOnlyList<ConversationMessage> messages)
    {
        if (string.IsNullOrWhiteSpace(userRequest))
        {
            return userRequest;
        }

        if (!ContainsConversationalReference(userRequest))
        {
            return userRequest;
        }

        var mentions = ExtractRecentRecipeMentions(messages);
        if (mentions.Count == 0)
        {
            return userRequest;
        }

        var isPortugueseReference = ContainsPortugueseReference(userRequest);
        var mentionText = string.Join(
            "; ",
            mentions.Select(mention => string.IsNullOrWhiteSpace(mention.RecipeId)
                ? mention.Title
                : $"{mention.RecipeId} ({mention.Title})"));
        var enrichedQuery = isPortugueseReference
            ? $"{userRequest} Receita referida: {mentionText}"
            : $"{userRequest} Referenced recipe: {mentionText}";

        _logger.LogInformation(
            "{AgentName} resolved conversational retrieval reference originalQuery={OriginalQuery} enrichedQuery={EnrichedQuery} referenceCount={ReferenceCount}",
            Name,
            userRequest,
            enrichedQuery,
            mentions.Count);

        return enrichedQuery;
    }

    private static bool ContainsConversationalReference(string userRequest)
    {
        return ConversationalReferencePhrases.Any(phrase =>
            userRequest.Contains(phrase, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsPortugueseReference(string userRequest)
    {
        return userRequest.Contains("receita", StringComparison.OrdinalIgnoreCase)
            || userRequest.Contains("prato", StringComparison.OrdinalIgnoreCase)
            || userRequest.Contains("sugeriste", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<RecipeMention> ExtractRecentRecipeMentions(IReadOnlyList<ConversationMessage> messages)
    {
        var mentions = new List<RecipeMention>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = messages.Count - 1; index >= 0; index--)
        {
            var message = messages[index];
            if (message.Role != ConversationRole.Assistant || string.IsNullOrWhiteSpace(message.Content))
            {
                continue;
            }

            foreach (Match match in SourceLineRegex.Matches(message.Content))
            {
                var recipeId = match.Groups["id"].Value.Trim();
                var title = match.Groups["title"].Value.Trim();
                if (string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                var key = string.IsNullOrWhiteSpace(recipeId)
                    ? title
                    : $"{recipeId}:{title}";
                if (!seen.Add(key))
                {
                    continue;
                }

                mentions.Add(new RecipeMention(recipeId, title));
            }

            if (mentions.Count > 0)
            {
                break;
            }
        }

        return mentions;
    }

    private sealed record RecipeMention(string RecipeId, string Title);

    private static bool ShouldUseRetrieval(IntentClassificationResult intent, bool retrievalAvailable)
    {
        if (!retrievalAvailable)
        {
            return false;
        }

        if (intent.Intent != IntentType.GeneralConversation)
        {
            return true;
        }

        // For low-confidence intent results, prefer retrieval-grounded generation.
        return intent.Confidence < 0.5;
    }

    private static string? ResolveRequestedLanguage(LanguageContext languageContext)
    {
        if (!string.IsNullOrWhiteSpace(languageContext.ExplicitLanguage))
        {
            return languageContext.ExplicitLanguage;
        }

        if (!string.IsNullOrWhiteSpace(languageContext.DetectedLanguage))
        {
            return languageContext.DetectedLanguage;
        }

        return languageContext.NegotiatedLanguages.FirstOrDefault(language => !string.IsNullOrWhiteSpace(language));
    }

    private static string FormatSources(IReadOnlyList<SourceAttribution> sources)
    {
        if (sources.Count == 0)
            return "Sources: none";

        return "Sources:\n" + string.Join("\n", sources.Select(source => $"- {source.RecipeId}: {source.Title}"));
    }

    private OrchestratorResult BuildResult(
        OrchestratorContext context,
        string response,
        IReadOnlyList<ConversationMessage> messagesToPersist,
        IReadOnlyList<string> executedTools,
        IReadOnlyList<RetrievedRecipeInfo> retrievedRecipes,
        IReadOnlyList<string> steps,
        IReadOnlyList<string> errors,
        Stopwatch startedAt)
    {
        startedAt.Stop();
        _logger.LogInformation("Orchestration path for {CorrelationId}: {ExecutionSteps}", context.CorrelationId, string.Join(" -> ", steps));
        return new OrchestratorResult(response, Name, executedTools, retrievedRecipes, steps, startedAt.Elapsed, errors, messagesToPersist);
    }
}