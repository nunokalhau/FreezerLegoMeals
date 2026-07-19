using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using RAG.DotNet;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace Services.DotNet;

public class AssistantService : IAssistantService
{
    private readonly IOllamaClient _ollamaClient;
    private readonly IConversationStore _conversationStore;
    private readonly IToolExecutor _toolExecutor;
    private readonly ILogger<AssistantService> _logger;
    private readonly AssistantOptions _options;
    private readonly IRetrievalService? _retrievalService;
    private readonly IPromptBuilder? _promptBuilder;

    public AssistantService(
        IOllamaClient ollamaClient,
        IConversationStore conversationStore,
        IToolExecutor toolExecutor,
        IOptions<AssistantOptions> options,
        ILogger<AssistantService> logger,
        IRetrievalService? retrievalService = null,
        IPromptBuilder? promptBuilder = null)
    {
        _ollamaClient = ollamaClient ?? throw new ArgumentNullException(nameof(ollamaClient));
        _conversationStore = conversationStore ?? throw new ArgumentNullException(nameof(conversationStore));
        _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _retrievalService = retrievalService;
        _promptBuilder = promptBuilder;
    }

    public async Task<AssistantChatResult> ChatAsync(string message, string? conversationId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message is required", nameof(message));

        var conversation = _conversationStore.GetOrCreateConversation(conversationId);
        var now = DateTimeOffset.UtcNow;
        var currentUserMessage = new ConversationMessage(ConversationRole.User, message, now);
        var messages = new List<ConversationMessage>
        {
            new(ConversationRole.System, _options.SystemPrompt, now)
        };
        messages.AddRange(conversation.Messages);
        messages.Add(currentUserMessage);
        var messagesToPersist = new List<ConversationMessage> { currentUserMessage };
        var tools = _toolExecutor.GetTools();
        var totalToolCalls = 0;
        var startedAt = Stopwatch.StartNew();

        while (true)
        {
            if (HasExceededExecutionLimits(messages, startedAt, out var limitError))
            {
                return PersistAndReturnError(conversation.ConversationId, messagesToPersist, limitError);
            }

            var assistantResult = await _ollamaClient.ChatAsync(null, messages, tools, cancellationToken);
            if (!assistantResult.HasToolCalls)
            {
                var content = assistantResult.Content;
                if (RequiresRepositoryKnowledge(message) && _retrievalService is not null && _promptBuilder is not null)
                {
                    content = await AnswerWithRetrievalAsync(message, cancellationToken);
                }

                var finalMessage = new ConversationMessage(ConversationRole.Assistant, content, DateTimeOffset.UtcNow);
                messagesToPersist.Add(finalMessage);
                _conversationStore.AppendMessages(conversation.ConversationId, messagesToPersist);
                _logger.LogInformation("Assistant request completed with {TotalToolCalls} tool calls", totalToolCalls);
                return new AssistantChatResult(conversation.ConversationId, content);
            }

            if (!string.IsNullOrWhiteSpace(assistantResult.Content))
            {
                var assistantMessage = new ConversationMessage(ConversationRole.Assistant, assistantResult.Content, DateTimeOffset.UtcNow);
                messages.Add(assistantMessage);
                messagesToPersist.Add(assistantMessage);
            }

            foreach (var toolCall in assistantResult.ToolCalls)
            {
                if (totalToolCalls >= _options.MaximumToolCallsPerRequest)
                {
                    return PersistAndReturnError(
                        conversation.ConversationId,
                        messagesToPersist,
                        $"The request could not be completed because it exceeded the maximum tool call limit of {_options.MaximumToolCallsPerRequest}.");
                }

                var toolStartedAt = Stopwatch.StartNew();
                totalToolCalls++;
                _logger.LogInformation("Assistant requested tool {ToolName} with arguments {ToolArguments}", toolCall.Name, JsonSerializer.Serialize(toolCall.Arguments));

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
                }
                finally
                {
                    toolStartedAt.Stop();
                }

                _logger.LogInformation(
                    "Assistant tool {ToolName} finished in {ExecutionDurationMs}ms with success={ToolSuccess}",
                    toolResult.Tool,
                    toolStartedAt.ElapsedMilliseconds,
                    toolResult.Success);

                var toolMessage = new ConversationMessage(
                    ConversationRole.Tool,
                    JsonSerializer.Serialize(toolResult),
                    DateTimeOffset.UtcNow);
                messages.Add(toolMessage);
                messagesToPersist.Add(toolMessage);
            }
        }
    }

    private bool HasExceededExecutionLimits(IReadOnlyList<ConversationMessage> messages, Stopwatch startedAt, out string error)
    {
        if (_options.MaximumConversationSize > 0 && messages.Count > _options.MaximumConversationSize)
        {
            error = $"The request could not be completed because the conversation exceeded the maximum size of {_options.MaximumConversationSize} messages.";
            return true;
        }

        if (_options.MaximumExecutionTime > TimeSpan.Zero && startedAt.Elapsed > _options.MaximumExecutionTime)
        {
            error = $"The request could not be completed because it exceeded the maximum execution time of {_options.MaximumExecutionTime}.";
            return true;
        }

        error = string.Empty;
        return false;
    }

    private AssistantChatResult PersistAndReturnError(string conversationId, List<ConversationMessage> messagesToPersist, string error)
    {
        var errorMessage = new ConversationMessage(ConversationRole.Assistant, error, DateTimeOffset.UtcNow);
        messagesToPersist.Add(errorMessage);
        _conversationStore.AppendMessages(conversationId, messagesToPersist);
        _logger.LogWarning("Assistant request failed gracefully: {AssistantError}", error);
        return new AssistantChatResult(conversationId, error);
    }

    private static bool RequiresRepositoryKnowledge(string message)
    {
        var normalized = message.ToLowerInvariant();
        string[] knowledgeTerms = ["recipe", "recipes", "meal", "meals", "cook", "cooking", "dinner", "lunch", "freezer", "ingredient", "ingredients", "prep", "preparation", "what can i", "what should i", "recommend"];
        return knowledgeTerms.Any(normalized.Contains);
    }

    private async Task<string> AnswerWithRetrievalAsync(string question, CancellationToken cancellationToken)
    {
        var retrieval = await _retrievalService!.RetrieveAsync(question, cancellationToken);
        if (retrieval.Recipes.Count == 0)
            return "The repository does not contain enough information to answer that question.\n\nSources: none";

        var prompt = _promptBuilder!.Build(question, retrieval.Recipes);
        var now = DateTimeOffset.UtcNow;
        var response = await _ollamaClient.ChatAsync(null, [
            new ConversationMessage(ConversationRole.System, _options.SystemPrompt, now),
            new ConversationMessage(ConversationRole.User, prompt, now)
        ], [], cancellationToken);
        var content = string.IsNullOrWhiteSpace(response.Content)
            ? "The repository does not contain enough information to answer that question."
            : response.Content.Trim();

        return $"{content}\n\n{FormatSources(retrieval.Sources)}";
    }

    private static string FormatSources(IReadOnlyList<SourceAttribution> sources)
    {
        if (sources.Count == 0)
            return "Sources: none";

        return "Sources:\n" + string.Join("\n", sources.Select(source => $"- {source.RecipeId}: {source.Title} (similarityScore: {source.SimilarityScore.ToString("F6", CultureInfo.InvariantCulture)})"));
    }
}