import { ConversationHistory, ConversationMessage } from './conversation-store';

export interface IMemoryProvider {
  getOrCreateConversation(conversationId?: string): Promise<ConversationHistory>;
  appendMessages(conversationId: string, messages: ConversationMessage[]): Promise<void>;
}