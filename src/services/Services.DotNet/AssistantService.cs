using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Orchestration.DotNet;

namespace Services.DotNet;

public class AssistantService : IAssistantService
{
    private readonly IConversationStore _conversationStore;
    private readonly IOrchestrator _orchestrator;
    private readonly ILogger<AssistantService> _logger;
    private readonly AssistantOptions _options;

    public AssistantService(
        IConversationStore conversationStore,
        IOrchestrator orchestrator,
        IOptions<AssistantOptions> options,
        ILogger<AssistantService> logger)
    {
        _conversationStore = conversationStore ?? throw new ArgumentNullException(nameof(conversationStore));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

        var context = new OrchestratorContext(
            message,
            now,
            Guid.NewGuid().ToString("N"),
            new Dictionary<string, object?>(),
            conversation.ConversationId,
            messages,
            messagesToPersist,
            _options);
        var result = await _orchestrator.ExecuteAsync(context, cancellationToken);

        _conversationStore.AppendMessages(conversation.ConversationId, result.MessagesToPersist);
        if (result.Errors.Count > 0)
            _logger.LogWarning("Assistant request completed with orchestration errors: {AssistantErrors}", string.Join("; ", result.Errors));

        return new AssistantChatResult(conversation.ConversationId, result.FinalResponse);
    }
}