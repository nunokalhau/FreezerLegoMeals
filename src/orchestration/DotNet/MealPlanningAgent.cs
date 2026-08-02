using System.Diagnostics;
using System.Text;
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
    private static readonly string[] MealPlanningPhrases =
    [
        "meal plan",
        "weekly plan",
        "plan meals",
        "plano de refeições",
        "plano semanal",
        "planear refeições",
        "planejar refeições",
        "planeia refeições",
        "planeie refeições"
    ];
    private const int StructuredMealPlanMaxAttempts = 3;
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

        if (ShouldUseStructuredMealPlanningWorkflow(context.UserRequest) && _retrievalService is not null)
        {
            try
            {
                steps.Add("StructuredMealPlanning");
                var structuredResponse = await ExecuteStructuredMealPlanningWorkflowAsync(
                    context,
                    executedTools,
                    retrievedRecipes,
                    steps,
                    errors,
                    activity,
                    cancellationToken);

                var finalMessage = new ConversationMessage(ConversationRole.Assistant, structuredResponse, DateTimeOffset.UtcNow);
                messagesToPersist.Add(finalMessage);
                activity?.SetTag("assistant.tool_call_count", executedTools.Count);
                activity?.SetTag("assistant.retrieved_recipe_count", retrievedRecipes.Count);
                activity?.SetTag("assistant.error_count", errors.Count);
                return BuildResult(context, structuredResponse, messagesToPersist, executedTools, retrievedRecipes, steps, errors, startedAt);
            }
            catch (Exception ex)
            {
                errors.Add(ex.Message);
                _logger.LogError(ex, "{AgentName} structured meal-planning workflow failed for correlation {CorrelationId}", Name, context.CorrelationId);
                var finalMessage = new ConversationMessage(ConversationRole.Assistant, InfrastructureFailureResponse, DateTimeOffset.UtcNow);
                messagesToPersist.Add(finalMessage);
                activity?.SetTag("assistant.tool_call_count", executedTools.Count);
                activity?.SetTag("assistant.retrieved_recipe_count", retrievedRecipes.Count);
                activity?.SetTag("assistant.error_count", errors.Count);
                return BuildResult(context, InfrastructureFailureResponse, messagesToPersist, executedTools, retrievedRecipes, steps, errors, startedAt);
            }
        }

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

    private static bool ShouldUseStructuredMealPlanningWorkflow(string userRequest)
    {
        if (string.IsNullOrWhiteSpace(userRequest))
        {
            return false;
        }

        return MealPlanningPhrases.Any(phrase => userRequest.Contains(phrase, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<string> ExecuteStructuredMealPlanningWorkflowAsync(
        OrchestratorContext context,
        IList<string> executedTools,
        IList<RetrievedRecipeInfo> retrievedRecipes,
        IList<string> steps,
        IList<string> errors,
        Activity? activity,
        CancellationToken cancellationToken)
    {
        if (_retrievalService is null)
        {
            return InfrastructureFailureResponse;
        }

        steps.Add("Semantic Search");
        steps.Add("Retrieval");
        steps.Add("Prompt Builder");
        steps.Add("RAG");

        var retrievalRequest = new RetrievalRequestContext(
            ResolveRetrievalQuery(context.UserRequest, context.Messages),
            new RetrievalIntentClassification(
                "MealPlanning",
                Confidence: 1,
                MatchedRule: "structured-meal-planning",
                DetectedLanguage: context.LocalizationOptions.PreferredLanguage),
            context.LocalizationOptions,
            context.LocalizationOptions.StrictMode);

        var retrieval = await _retrievalService.RetrieveAsync(retrievalRequest, cancellationToken);
        executedTools.Add(RetrievalCapabilityToolName);
        foreach (var source in retrieval.Sources)
        {
            retrievedRecipes.Add(new RetrievedRecipeInfo(source.RecipeId, source.Title, source.RetrievalScore));
        }

        activity?.SetTag("retrieval.profile_id", retrieval.Profile?.ProfileId ?? string.Empty);
        activity?.SetTag("retrieval.normalization_version", retrieval.NormalizationVersion);
        activity?.SetTag("retrieval.recipe_count", retrieval.Recipes.Count);

        if (retrieval.Recipes.Count == 0)
        {
            return IsPortugueseLanguage(context.LocalizationOptions.PreferredLanguage)
                ? "Não encontrei receitas suficientes no repositório para montar um plano de refeições com dados válidos."
                : "I could not find enough recipes in the repository to build a data-backed meal plan.";
        }

        var recipesById = retrieval.Recipes
            .GroupBy(recipe => recipe.RecipeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        steps.Add("Ollama");
        steps.Add("Structured Output Validation");
        var selectionResult = await RequestStructuredMealPlanSelectionAsync(
            context.UserRequest,
            retrieval.Recipes,
            context.LocalizationOptions.PreferredLanguage,
            recipesById,
            cancellationToken);

        if (selectionResult.Selection is null)
        {
            if (!string.IsNullOrWhiteSpace(selectionResult.ValidationError))
            {
                errors.Add(selectionResult.ValidationError);
            }

            return IsPortugueseLanguage(context.LocalizationOptions.PreferredLanguage)
                ? "Não foi possível validar um plano estruturado com IDs reais de receitas. Tenta novamente."
                : "I could not validate a structured meal plan with real recipe IDs. Please try again.";
        }

        steps.Add("Answer");
        return RenderStructuredMealPlanResponse(selectionResult.Selection, selectionResult.NormalizedJson, recipesById, context.LocalizationOptions.PreferredLanguage);
    }

    private async Task<StructuredMealPlanSelectionResult> RequestStructuredMealPlanSelectionAsync(
        string userRequest,
        IReadOnlyList<RetrievalRecipe> recipes,
        string preferredLanguage,
        IReadOnlyDictionary<string, RetrievalRecipe> recipesById,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var messages = new List<ConversationMessage>
        {
            new(
                ConversationRole.System,
                "You are a strict meal-planning selector. You must output JSON only and select only recipe IDs from the provided list.",
                now),
            new(
                ConversationRole.User,
                BuildStructuredMealPlanPrompt(userRequest, recipes, preferredLanguage),
                now)
        };

        string? lastError = null;
        for (var attempt = 1; attempt <= StructuredMealPlanMaxAttempts; attempt++)
        {
            var result = await _ollamaClient.ChatAsync(null, messages, [], cancellationToken);
            var rawContent = result.Content;

            if (TryParseStructuredMealPlanSelection(rawContent, recipesById, out var selection, out var validationError, out var normalizedJson))
            {
                return new StructuredMealPlanSelectionResult(selection, normalizedJson, null);
            }

            lastError = validationError;
            messages.Add(new ConversationMessage(ConversationRole.Assistant, rawContent, DateTimeOffset.UtcNow));
            messages.Add(new ConversationMessage(
                ConversationRole.User,
                $"Invalid output: {validationError}. Return only valid JSON with allowed recipeId values.",
                DateTimeOffset.UtcNow));
        }

        return new StructuredMealPlanSelectionResult(null, string.Empty, lastError ?? "Structured planner output could not be validated.");
    }

    private static bool TryParseStructuredMealPlanSelection(
        string rawContent,
        IReadOnlyDictionary<string, RetrievalRecipe> recipesById,
        out StructuredMealPlanSelection? selection,
        out string validationError,
        out string normalizedJson)
    {
        selection = null;
        normalizedJson = string.Empty;

        if (string.IsNullOrWhiteSpace(rawContent))
        {
            validationError = "Planner response is empty.";
            return false;
        }

        var json = ExtractJsonObject(rawContent);
        if (string.IsNullOrWhiteSpace(json))
        {
            validationError = "Planner response does not contain a valid JSON object.";
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("days", out var daysElement) || daysElement.ValueKind != JsonValueKind.Array)
            {
                validationError = "JSON must contain a 'days' array.";
                return false;
            }

            var days = new List<StructuredMealPlanDay>();
            var usedDayNumbers = new HashSet<int>();
            foreach (var dayElement in daysElement.EnumerateArray())
            {
                if (!dayElement.TryGetProperty("day", out var dayNumberElement) || !dayNumberElement.TryGetInt32(out var dayNumber) || dayNumber <= 0)
                {
                    validationError = "Each day entry must include a positive integer 'day'.";
                    return false;
                }

                if (!usedDayNumbers.Add(dayNumber))
                {
                    validationError = $"Day '{dayNumber}' is duplicated.";
                    return false;
                }

                if (!TryParseStructuredMealSlot(dayElement, "lunch", recipesById, out var lunch, out validationError))
                {
                    return false;
                }

                if (!TryParseStructuredMealSlot(dayElement, "dinner", recipesById, out var dinner, out validationError))
                {
                    return false;
                }

                if (lunch is null && dinner is null)
                {
                    validationError = $"Day '{dayNumber}' must include at least one meal slot (lunch or dinner).";
                    return false;
                }

                days.Add(new StructuredMealPlanDay(dayNumber, lunch, dinner));
            }

            if (days.Count == 0)
            {
                validationError = "The 'days' array must contain at least one day.";
                return false;
            }

            selection = new StructuredMealPlanSelection(days.OrderBy(day => day.Day).ToArray());
            normalizedJson = JsonSerializer.Serialize(new
            {
                days = selection.Days.Select(day => new
                {
                    day = day.Day,
                    lunch = day.Lunch is null ? null : new { recipeId = day.Lunch.RecipeId },
                    dinner = day.Dinner is null ? null : new { recipeId = day.Dinner.RecipeId }
                })
            });
            validationError = string.Empty;
            return true;
        }
        catch (JsonException ex)
        {
            validationError = $"Planner JSON is invalid: {ex.Message}";
            return false;
        }
    }

    private static bool TryParseStructuredMealSlot(
        JsonElement dayElement,
        string slotName,
        IReadOnlyDictionary<string, RetrievalRecipe> recipesById,
        out StructuredMealSlot? slot,
        out string validationError)
    {
        slot = null;

        if (!dayElement.TryGetProperty(slotName, out var slotElement) || slotElement.ValueKind == JsonValueKind.Null)
        {
            validationError = string.Empty;
            return true;
        }

        if (slotElement.ValueKind != JsonValueKind.Object || !slotElement.TryGetProperty("recipeId", out var recipeIdElement))
        {
            validationError = $"'{slotName}' must be an object with 'recipeId'.";
            return false;
        }

        var recipeId = recipeIdElement.ValueKind switch
        {
            JsonValueKind.String => recipeIdElement.GetString(),
            JsonValueKind.Number => recipeIdElement.GetRawText(),
            _ => null
        };

        if (string.IsNullOrWhiteSpace(recipeId))
        {
            validationError = $"'{slotName}.recipeId' is required.";
            return false;
        }

        if (!recipesById.TryGetValue(recipeId.Trim(), out var recipe))
        {
            validationError = $"'{slotName}.recipeId' must reference an existing retrieved recipe ID. Received '{recipeId}'.";
            return false;
        }

        slot = new StructuredMealSlot(recipe.RecipeId);
        validationError = string.Empty;
        return true;
    }

    private static string BuildStructuredMealPlanPrompt(string userRequest, IReadOnlyList<RetrievalRecipe> recipes, string preferredLanguage)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Build a meal plan for the user request using only the recipe IDs listed below.");
        builder.AppendLine($"User request: {userRequest}");
        builder.AppendLine($"Preferred language: {preferredLanguage}");
        builder.AppendLine();
        builder.AppendLine("Allowed recipes:");
        foreach (var recipe in recipes)
        {
            builder.AppendLine($"- {recipe.RecipeId}: {recipe.Title}");
        }

        builder.AppendLine();
        builder.AppendLine("Rules:");
        builder.AppendLine("1) Return JSON only. No markdown, no prose, no explanations.");
        builder.AppendLine("2) Use only these fields: days, day, lunch, dinner, recipeId.");
        builder.AppendLine("3) recipeId values must be selected only from the allowed recipe IDs.");
        builder.AppendLine("4) Do not invent side dishes, ingredients, times, categories, or new recipes.");
        builder.AppendLine("5) You may reuse recipe IDs across meals when needed.");
        builder.AppendLine();
        builder.AppendLine("Output format example:");
        builder.AppendLine("{\"days\":[{\"day\":1,\"lunch\":{\"recipeId\":\"2\"},\"dinner\":{\"recipeId\":\"7\"}}]}");
        return builder.ToString().TrimEnd();
    }

    private static string RenderStructuredMealPlanResponse(
        StructuredMealPlanSelection selection,
        string normalizedJson,
        IReadOnlyDictionary<string, RetrievalRecipe> recipesById,
        string preferredLanguage)
    {
        var isPortuguese = IsPortugueseLanguage(preferredLanguage);
        var builder = new StringBuilder();

        builder.AppendLine(isPortuguese ? "Plano de refeições validado:" : "Validated meal plan:");

        foreach (var day in selection.Days)
        {
            builder.AppendLine(isPortuguese ? $"Dia {day.Day}" : $"Day {day.Day}");
            if (day.Lunch is not null)
            {
                var lunchRecipe = recipesById[day.Lunch.RecipeId];
                builder.AppendLine(isPortuguese
                    ? $"- Almoço: {lunchRecipe.Title} (recipeId: {lunchRecipe.RecipeId})"
                    : $"- Lunch: {lunchRecipe.Title} (recipeId: {lunchRecipe.RecipeId})");
            }

            if (day.Dinner is not null)
            {
                var dinnerRecipe = recipesById[day.Dinner.RecipeId];
                builder.AppendLine(isPortuguese
                    ? $"- Jantar: {dinnerRecipe.Title} (recipeId: {dinnerRecipe.RecipeId})"
                    : $"- Dinner: {dinnerRecipe.Title} (recipeId: {dinnerRecipe.RecipeId})");
            }
        }

        builder.AppendLine();
        builder.AppendLine(isPortuguese ? "JSON estruturado validado:" : "Validated structured JSON:");
        builder.AppendLine(normalizedJson);
        return builder.ToString().TrimEnd();
    }

    private static bool IsPortugueseLanguage(string? language)
    {
        return !string.IsNullOrWhiteSpace(language)
            && language.StartsWith("pt", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractJsonObject(string content)
    {
        var start = content.IndexOf('{');
        var end = content.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return null;
        }

        return content[start..(end + 1)];
    }

    private sealed record StructuredMealSlot(string RecipeId);

    private sealed record StructuredMealPlanDay(int Day, StructuredMealSlot? Lunch, StructuredMealSlot? Dinner);

    private sealed record StructuredMealPlanSelection(IReadOnlyList<StructuredMealPlanDay> Days);

    private sealed record StructuredMealPlanSelectionResult(
        StructuredMealPlanSelection? Selection,
        string NormalizedJson,
        string? ValidationError);

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