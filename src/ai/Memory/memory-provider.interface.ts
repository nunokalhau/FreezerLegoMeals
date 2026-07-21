import { ConversationHistory, ConversationMessage } from '../../services/Services.NestJS/conversation-store';

/**
 * Interface for conversation memory providers.
 * Matches the .NET conversation memory provider pattern.
 */
export interface IMemoryProvider {
  /**
   * Gets or creates a conversation with the specified ID.
   * @param conversationId The ID of the conversation to get or create (optional)
   * @returns The conversation history
   */
  getOrCreateConversation(conversationId?: string): Promise<ConversationHistory>;

  /**
   * Appends messages to an existing conversation.
   * @param conversationId The ID of the conversation to append to
   * @param messages The messages to append
   */
  appendMessages(conversationId: string, messages: ConversationMessage[]): Promise<void>;
}