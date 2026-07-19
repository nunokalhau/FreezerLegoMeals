import { Inject, Injectable, Optional } from '@nestjs/common';
import { randomUUID } from 'crypto';

export type ConversationRole = 'System' | 'User' | 'Assistant';

export interface ConversationMessage {
  role: ConversationRole;
  content: string;
  timestamp: Date;
}

export interface ConversationHistory {
  conversationId: string;
  messages: ConversationMessage[];
}

export interface ConversationStoreOptions {
  maximumConversations: number;
  maximumMessagesPerConversation: number;
  automaticTrimming: boolean;
  expirationTimeoutMs: number;
}

export const CONVERSATION_STORE_OPTIONS = 'CONVERSATION_STORE_OPTIONS';

export abstract class ConversationStore {
  abstract getOrCreateConversation(conversationId?: string): ConversationHistory;
  abstract appendMessages(conversationId: string, messages: ConversationMessage[]): void;
}

export function createConversationStoreOptions(): ConversationStoreOptions {
  return {
    maximumConversations: Number(process.env.ASSISTANT_MAXIMUM_CONVERSATIONS || 1000),
    maximumMessagesPerConversation: Number(process.env.ASSISTANT_MAXIMUM_MESSAGES_PER_CONVERSATION || 50),
    automaticTrimming: (process.env.ASSISTANT_AUTOMATIC_TRIMMING || 'true').toLowerCase() === 'true',
    expirationTimeoutMs: Number(process.env.ASSISTANT_CONVERSATION_EXPIRATION_MS || 3600000),
  };
}

interface ConversationState {
  messages: ConversationMessage[];
  lastAccessedAt: number;
}

// TODO: Replace InMemoryConversationStore with a Redis-backed implementation.
// TODO: Persist conversation history using Redis running in Docker.
// TODO: Support distributed conversation storage for multiple API instances.
@Injectable()
export class InMemoryConversationStore implements ConversationStore {
  private readonly conversations = new Map<string, ConversationState>();
  private readonly options: ConversationStoreOptions;

  constructor(
    @Optional()
    @Inject(CONVERSATION_STORE_OPTIONS)
    options?: ConversationStoreOptions
  ) {
    this.options = options ?? createConversationStoreOptions();
  }

  getOrCreateConversation(conversationId?: string): ConversationHistory {
    this.expireOldConversations();

    const resolvedConversationId = conversationId?.trim() || randomUUID();
    const state = this.conversations.get(resolvedConversationId) ?? {
      messages: [],
      lastAccessedAt: Date.now(),
    };

    state.lastAccessedAt = Date.now();
    this.conversations.set(resolvedConversationId, state);

    return {
      conversationId: resolvedConversationId,
      messages: [...state.messages],
    };
  }

  appendMessages(conversationId: string, messages: ConversationMessage[]): void {
    if (!conversationId?.trim()) {
      throw new Error('Conversation ID is required');
    }

    const state = this.conversations.get(conversationId) ?? {
      messages: [],
      lastAccessedAt: Date.now(),
    };
    state.messages.push(...messages);
    state.lastAccessedAt = Date.now();

    if (this.options.automaticTrimming && this.options.maximumMessagesPerConversation > 0) {
      state.messages = state.messages.slice(-this.options.maximumMessagesPerConversation);
    }

    this.conversations.set(conversationId, state);
    this.enforceConversationLimit();
  }

  private expireOldConversations(): void {
    if (this.options.expirationTimeoutMs <= 0) {
      return;
    }

    const expiresBefore = Date.now() - this.options.expirationTimeoutMs;
    for (const [conversationId, state] of this.conversations) {
      if (state.lastAccessedAt < expiresBefore) {
        this.conversations.delete(conversationId);
      }
    }
  }

  private enforceConversationLimit(): void {
    if (this.options.maximumConversations <= 0) {
      return;
    }

    const excessCount = this.conversations.size - this.options.maximumConversations;
    if (excessCount <= 0) {
      return;
    }

    [...this.conversations.entries()]
      .sort(([, left], [, right]) => left.lastAccessedAt - right.lastAccessedAt)
      .slice(0, excessCount)
      .forEach(([conversationId]) => this.conversations.delete(conversationId));
  }
}