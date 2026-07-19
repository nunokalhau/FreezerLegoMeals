namespace Services.DotNet;

public interface IConversationStore
{
    ConversationHistory GetOrCreateConversation(string? conversationId = null);

    void AppendMessages(string conversationId, IEnumerable<ConversationMessage> messages);
}