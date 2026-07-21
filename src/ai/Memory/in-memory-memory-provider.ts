import { Injectable } from '@nestjs/common';
import { ConversationStore, ConversationHistory, ConversationMessage } from '../../services/Services.NestJS/conversation-store';
import { IMemoryProvider } from './memory-provider.interface';

/**
 * In-memory implementation of the memory provider interface.
 * This is an adapter to maintain backwards compatibility with existing InMemoryConversationStore.
 */
@Injectable()
export class InMemoryMemoryProvider implements IMemoryProvider {
  constructor(private readonly conversationStore: ConversationStore) {}

  async getOrCreateConversation(conversationId?: string): Promise<ConversationHistory> {
    return this.conversationStore.getOrCreateConversation(conversationId);
  }

  async appendMessages(conversationId: string, messages: ConversationMessage[]): Promise<void> {
    this.conversationStore.appendMessages(conversationId, messages);
  }
}