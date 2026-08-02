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
    private const string RetrievalCapabilityToolName = "retrieve_repository_context";
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
    private readonly IRetrievalService? _retrievalService;
    private readonly IPromptBuilder? _promptBuilder;

    public MealPlanningAgent(
        IOllamaClient ollamaClient,
        IToolExecutor toolExecutor,
        IRoutingPolicy routingPolicy,
        ILogger<MealPlanningAgent> logger,
        IRetrievalService? retrievalService = null,
        IPromptBuilder? promptBuilder = null)
    {
        _ollamaClient = ollamaClient ?? throw new ArgumentNullException(nameof(ollamaClient));
        _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
        _routingPolicy = routingPolicy ?? throw new ArgumentNullException(nameof(routingPolicy));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _retrievalService = retrievalService;
        _promptBuilder = promptBuilder;
    }

    public string Name => "MealPlanningAgent";

    public bool CanHandle(OrchestratorContext context) => true;

    public async Task<OrchestratorResult> ExecuteAsync(OrchestratorContext context, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("orchestration.agent.execute", ActivityKind.Internal);
        activity?.SetTag("agent.name", Name);
        activity?.SetTag("assistant.correlation_id", context.CorrelationId);
        activity?.SetTag("assistant.user_request_length", context.UserRequest.Length);

        var startedAt = Stopwatch.StartNew();
        var messages = context.Messages.ToList();
        var messagesToPersist = context.MessagesToPersist.ToList();
        var tools = BuildAvailableTools();
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

            var route = _routingPolicy.DetermineAssistantRoute(context, assistantResult, retrievalAvailable: _retrievalService is not null);
            _logger.LogInformation(
                "{AgentName} routing decision correlation={CorrelationId} route={Route} hasToolCalls={HasToolCalls} retrievalAvailable={RetrievalAvailable}",
                Name,
                context.CorrelationId,
                route,
                assistantResult.HasToolCalls,
                _retrievalService is not null);

            if (route != AssistantRoute.InvokeTools)
            {
                var content = string.IsNullOrWhiteSpace(assistantResult.Content)
                    ? InfrastructureFailureResponse
                    : assistantResult.Content.Trim();

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
                    if (IsInternalRetrievalToolCall(toolCall.Name))
                    {
                        steps.Add("Semantic Search");
                        steps.Add("Retrieval");
                        steps.Add("Prompt Builder");
                        steps.Add("RAG");
                        var retrievalToolResult = await ExecuteRetrievalCapabilityAsync(
                            context,
                            toolCall.Arguments,
                            messages,
                            activity,
                            cancellationToken);
                        toolResult = retrievalToolResult.Result;
                        retrievedRecipes.AddRange(retrievalToolResult.RetrievedRecipes);
                    }
                    else
                    {
                        toolResult = await _toolExecutor.ExecuteAsync(toolCall.Name, toolCall.Arguments, cancellationToken);
                    }
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

    private IReadOnlyList<ToolDefinition> BuildAvailableTools()
    {
        var externalTools = _toolExecutor.GetTools();
        if (_retrievalService is null)
        {
            return externalTools;
        }

        var combined = new List<ToolDefinition>(externalTools.Count + 1) { CreateRetrievalCapabilityToolDefinition() };
        combined.AddRange(externalTools);
        return combined;
    }

    private static bool IsInternalRetrievalToolCall(string toolName)
    {
        return string.Equals(toolName, RetrievalCapabilityToolName, StringComparison.Ordinal);
    }

    private static ToolDefinition CreateRetrievalCapabilityToolDefinition()
    {
        return new ToolDefinition
        {
            Name = RetrievalCapabilityToolName,
            Description = "Retrieve repository recipe context for the current conversation when factual recipe grounding is needed. Use this before answering recipe availability, recommendations, details, meal plans, dietary constraints, or ingredient-based requests.",
            Parameters = ["query", "intent"],
            OutputDescription = "Structured retrieval context with ranked recipes and source attributions to support the final grounded answer."
        };
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

    private async Task<(ToolExecutionResult Result, IReadOnlyList<RetrievedRecipeInfo> RetrievedRecipes)> ExecuteRetrievalCapabilityAsync(
        OrchestratorContext context,
        IReadOnlyDictionary<string, object?> arguments,
        IReadOnlyList<ConversationMessage> messages,
        Activity? activity,
        CancellationToken cancellationToken)
    {
        if (_retrievalService is null)
        {
            return (new ToolExecutionResult
            {
                Success = false,
                Tool = RetrievalCapabilityToolName,
                Error = "Retrieval service is not configured."
            }, []);
        }

        var requestedQuery = GetStringArgument(arguments, "query") ?? context.UserRequest;
        var requestedIntent = GetStringArgument(arguments, "intent") ?? "GeneralConversation";
        var retrievalQuery = ResolveRetrievalQuery(requestedQuery, messages);
        activity?.SetTag("retrieval.original_user_query", requestedQuery);
        activity?.SetTag("retrieval.effective_query", retrievalQuery);

        var retrievalRequest = new RetrievalRequestContext(
            retrievalQuery,
            new RetrievalIntentClassification(
                requestedIntent,
                Confidence: 1,
                MatchedRule: "llm-planner",
                DetectedLanguage: context.LocalizationOptions.PreferredLanguage),
            context.LocalizationOptions,
            context.LocalizationOptions.StrictMode);
        var retrieval = await _retrievalService.RetrieveAsync(retrievalRequest, cancellationToken);
        activity?.SetTag("retrieval.profile_id", retrieval.Profile?.ProfileId ?? string.Empty);
        activity?.SetTag("retrieval.profile_family", retrieval.Profile?.ProfileFamily.ToString() ?? string.Empty);
        activity?.SetTag("retrieval.normalization_version", retrieval.NormalizationVersion);
        var retrievedRecipes = retrieval.Sources
            .Select(source => new RetrievedRecipeInfo(source.RecipeId, source.Title, source.RetrievalScore))
            .ToList();

        var prompt = _promptBuilder is null
            ? string.Empty
            : _promptBuilder.Build(
                requestedQuery,
                retrieval.Recipes,
                requestedIntent,
                context.LocalizationOptions,
                ResolveRequestedLanguage(context.LanguageContext));

        _logger.LogInformation(
            "{AgentName} retrieval capability executed correlation={CorrelationId} userQuestion={UserQuestion} retrievedCount={RetrievedCount} recipes={RecipeSummaries}",
            Name,
            context.CorrelationId,
            requestedQuery,
            retrieval.Recipes.Count,
            string.Join(" | ", retrieval.Recipes.Select(recipe => $"{recipe.RecipeId}:{recipe.Title}")));

        var output = new
        {
            query = requestedQuery,
            effectiveQuery = retrievalQuery,
            retrievalProfile = retrieval.Profile?.ProfileId,
            normalizationVersion = retrieval.NormalizationVersion,
            sources = retrieval.Sources.Select(source => new
            {
                recipeId = source.RecipeId,
                title = source.Title,
                retrievalScore = source.RetrievalScore
            }),
            recipes = retrieval.Recipes.Select(recipe => new
            {
                recipeId = recipe.RecipeId,
                canonicalRecipeId = recipe.CanonicalRecipeId,
                title = recipe.Title,
                description = recipe.Description,
                tags = recipe.Tags,
                ingredients = recipe.Ingredients,
                preparationSteps = recipe.PreparationSteps,
                cookingTime = recipe.CookingTime
            }),
            promptContext = prompt
        };

        return (new ToolExecutionResult
        {
            Success = true,
            Tool = RetrievalCapabilityToolName,
            Output = output
        }, retrievedRecipes);
    }

    private static string? GetStringArgument(IReadOnlyDictionary<string, object?> arguments, string key)
    {
        if (!arguments.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        if (value is string raw)
        {
            return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
        }

        if (value is JsonElement element)
        {
            return element.ValueKind == JsonValueKind.String
                ? element.GetString()
                : element.GetRawText();
        }

        var text = value.ToString();
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
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