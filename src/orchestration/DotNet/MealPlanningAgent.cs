using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RAG.DotNet;
using Services.DotNet;

namespace Orchestration.DotNet;

public sealed class MealPlanningAgent : IAgent
{
    private readonly IOllamaClient _ollamaClient;
    private readonly IToolExecutor _toolExecutor;
    private readonly ILogger<MealPlanningAgent> _logger;
    private readonly IRetrievalService? _retrievalService;
    private readonly IPromptBuilder? _promptBuilder;

    public MealPlanningAgent(
        IOllamaClient ollamaClient,
        IToolExecutor toolExecutor,
        ILogger<MealPlanningAgent> logger,
        IRetrievalService? retrievalService = null,
        IPromptBuilder? promptBuilder = null)
    {
        _ollamaClient = ollamaClient ?? throw new ArgumentNullException(nameof(ollamaClient));
        _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _retrievalService = retrievalService;
        _promptBuilder = promptBuilder;
    }

    public string Name => "MealPlanningAgent";

    public bool CanHandle(OrchestratorContext context) => true;

    public async Task<OrchestratorResult> ExecuteAsync(OrchestratorContext context, CancellationToken cancellationToken = default)
    {
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
            if (!assistantResult.HasToolCalls)
            {
                var content = assistantResult.Content;
                if (RequiresRepositoryKnowledge(context.UserRequest) && _retrievalService is not null && _promptBuilder is not null)
                {
                    steps.Add("Semantic Search");
                    steps.Add("Retrieval");
                    steps.Add("Prompt Builder");
                    steps.Add("RAG");
                    var ragResult = await AnswerWithRetrievalAsync(context, cancellationToken);
                    content = ragResult.Response;
                    retrievedRecipes.AddRange(ragResult.RetrievedRecipes);
                }

                steps.Add("Answer");
                var finalMessage = new ConversationMessage(ConversationRole.Assistant, content, DateTimeOffset.UtcNow);
                messagesToPersist.Add(finalMessage);
                _logger.LogInformation("{AgentName} completed with {TotalToolCalls} tool calls", Name, totalToolCalls);
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

    private static bool RequiresRepositoryKnowledge(string message)
    {
        var normalized = message.ToLowerInvariant();
        string[] knowledgeTerms = ["recipe", "recipes", "meal", "meals", "cook", "cooking", "dinner", "lunch", "freezer", "ingredient", "ingredients", "prep", "preparation", "what can i", "what should i", "recommend"];
        return knowledgeTerms.Any(normalized.Contains);
    }

    private async Task<(string Response, IReadOnlyList<RetrievedRecipeInfo> RetrievedRecipes)> AnswerWithRetrievalAsync(OrchestratorContext context, CancellationToken cancellationToken)
    {
        var retrieval = await _retrievalService!.RetrieveAsync(context.UserRequest, cancellationToken);
        var retrievedRecipes = retrieval.Sources
            .Select(source => new RetrievedRecipeInfo(source.RecipeId, source.Title, source.SimilarityScore))
            .ToList();
        if (retrieval.Recipes.Count == 0)
            return ("The repository does not contain enough information to answer that question.\n\nSources: none", retrievedRecipes);

        var prompt = _promptBuilder!.Build(context.UserRequest, retrieval.Recipes);
        var now = DateTimeOffset.UtcNow;
        var response = await _ollamaClient.ChatAsync(null, [
            new ConversationMessage(ConversationRole.System, context.AssistantOptions.SystemPrompt, now),
            new ConversationMessage(ConversationRole.User, prompt, now)
        ], [], cancellationToken);
        var content = string.IsNullOrWhiteSpace(response.Content)
            ? "The repository does not contain enough information to answer that question."
            : response.Content.Trim();

        return ($"{content}\n\n{FormatSources(retrieval.Sources)}", retrievedRecipes);
    }

    private static string FormatSources(IReadOnlyList<SourceAttribution> sources)
    {
        if (sources.Count == 0)
            return "Sources: none";

        return "Sources:\n" + string.Join("\n", sources.Select(source => $"- {source.RecipeId}: {source.Title} (similarityScore: {source.SimilarityScore.ToString("F6", CultureInfo.InvariantCulture)})"));
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