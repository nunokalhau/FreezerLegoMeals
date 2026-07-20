using Services.DotNet;

namespace AI.Memory.DotNet;

public interface IMemoryProvider
{
    ConversationHistory GetOrCreateConversation(string? conversationId = null);
    
    void AppendMessages(string conversationId, IEnumerable<ConversationMessage> messages);
}