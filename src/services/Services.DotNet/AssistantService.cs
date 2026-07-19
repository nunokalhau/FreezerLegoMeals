using Microsoft.Extensions.Options;

namespace Services.DotNet;

public class AssistantService : IAssistantService
{
    private readonly IOllamaClient _ollamaClient;
    private readonly IConversationStore _conversationStore;
    private readonly AssistantOptions _options;

    public AssistantService(
        IOllamaClient ollamaClient,
        IConversationStore conversationStore,
        IOptions<AssistantOptions> options)
    {
        _ollamaClient = ollamaClient ?? throw new ArgumentNullException(nameof(ollamaClient));
        _conversationStore = conversationStore ?? throw new ArgumentNullException(nameof(conversationStore));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
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

        var assistantResponse = await _ollamaClient.ChatAsync(null, messages, cancellationToken);

        _conversationStore.AppendMessages(conversation.ConversationId, [
            currentUserMessage,
            new ConversationMessage(ConversationRole.Assistant, assistantResponse, DateTimeOffset.UtcNow)
        ]);

        return new AssistantChatResult(conversation.ConversationId, assistantResponse);
    }
}