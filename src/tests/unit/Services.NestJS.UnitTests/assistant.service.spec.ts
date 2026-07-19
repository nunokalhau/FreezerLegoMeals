import { AssistantService } from '../../../services/Services.NestJS/assistant.service';
import { ConversationStore } from '../../../services/Services.NestJS/conversation-store';
import { OllamaClient } from '../../../services/Services.NestJS/ollama.client';

describe('AssistantService', () => {
  it('creates a conversation and persists messages', async () => {
    const ollamaClient = {
      chat: jest.fn().mockResolvedValue('assistant response'),
    } as unknown as jest.Mocked<OllamaClient>;
    const conversationStore = new FakeConversationStore();
    const service = new AssistantService(ollamaClient, conversationStore, { systemPrompt: 'system prompt' });

    const result = await service.chat('Hello');

    expect(result).toEqual({ conversationId: 'conversation-1', response: 'assistant response' });
    expect(ollamaClient.chat).toHaveBeenCalledWith(undefined, [
      expect.objectContaining({ role: 'System', content: 'system prompt' }),
      expect.objectContaining({ role: 'User', content: 'Hello' }),
    ]);
    expect(conversationStore.messages.map((message) => message.content)).toEqual(['Hello', 'assistant response']);
  });

  it('includes previous conversation history', async () => {
    const ollamaClient = {
      chat: jest.fn().mockResolvedValue('second response'),
    } as unknown as jest.Mocked<OllamaClient>;
    const conversationStore = new FakeConversationStore([
      { role: 'User', content: 'First', timestamp: new Date() },
      { role: 'Assistant', content: 'First response', timestamp: new Date() },
    ]);
    const service = new AssistantService(ollamaClient, conversationStore, { systemPrompt: 'system prompt' });

    const result = await service.chat('Second', 'conversation-1');

    expect(result.conversationId).toBe('conversation-1');
    expect(ollamaClient.chat.mock.calls[0][1].map((message) => message.content)).toEqual([
      'system prompt',
      'First',
      'First response',
      'Second',
    ]);
  });
});

class FakeConversationStore extends ConversationStore {
  constructor(public messages = []) {
    super();
  }

  getOrCreateConversation(conversationId?: string) {
    return {
      conversationId: conversationId || 'conversation-1',
      messages: [...this.messages],
    };
  }

  appendMessages(_conversationId: string, messages) {
    this.messages.push(...messages);
  }
}