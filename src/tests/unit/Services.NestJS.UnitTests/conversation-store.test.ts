import { InMemoryConversationStore } from '../../../services/Services.NestJS/conversation-store';

describe('InMemoryConversationStore', () => {
  it('creates a new conversation when no id is provided', () => {
    const store = createStore();

    const conversation = store.getOrCreateConversation();

    expect(conversation.conversationId).toBeTruthy();
    expect(conversation.messages).toEqual([]);
  });

  it('retrieves an existing conversation with persisted messages', () => {
    const store = createStore();
    store.appendMessages('conversation-1', [createMessage('hello')]);

    const conversation = store.getOrCreateConversation('conversation-1');

    expect(conversation.conversationId).toBe('conversation-1');
    expect(conversation.messages.map((message) => message.content)).toEqual(['hello']);
  });

  it('persists messages in order', () => {
    const store = createStore();

    store.appendMessages('conversation-1', [createMessage('first'), createMessage('second')]);

    expect(store.getOrCreateConversation('conversation-1').messages.map((message) => message.content)).toEqual(['first', 'second']);
  });

  it('trims old messages when the limit is exceeded', () => {
    const store = createStore({ maximumMessagesPerConversation: 2 });

    store.appendMessages('conversation-1', [createMessage('first'), createMessage('second'), createMessage('third')]);

    expect(store.getOrCreateConversation('conversation-1').messages.map((message) => message.content)).toEqual(['second', 'third']);
  });

  it('expires old conversations', async () => {
    const store = createStore({ expirationTimeoutMs: 1 });
    store.appendMessages('conversation-1', [createMessage('old')]);

    await new Promise((resolve) => setTimeout(resolve, 5));

    expect(store.getOrCreateConversation('conversation-1').messages).toEqual([]);
  });

  it('supports multiple simultaneous conversations', () => {
    const store = createStore();

    store.appendMessages('conversation-1', [createMessage('first')]);
    store.appendMessages('conversation-2', [createMessage('second')]);

    expect(store.getOrCreateConversation('conversation-1').messages[0].content).toBe('first');
    expect(store.getOrCreateConversation('conversation-2').messages[0].content).toBe('second');
  });
});

function createStore(options = {}) {
  return new InMemoryConversationStore({
    maximumConversations: 1000,
    maximumMessagesPerConversation: 50,
    automaticTrimming: true,
    expirationTimeoutMs: 3600000,
    ...options,
  });
}

function createMessage(content: string) {
  return { role: 'User' as const, content, timestamp: new Date() };
}