import { AssistantService } from '../../../services/Services.NestJS/assistant.service';
import { ConversationStore } from '../../../services/Services.NestJS/conversation-store';
import { OllamaClient } from '../../../services/Services.NestJS/ollama.client';
import { ToolExecutor } from '../../../services/Services.NestJS/tool-executor';
import { PromptBuilder } from '../../../ai/RAG/NestJS/prompt-builder';
import { RetrievalService } from '../../../ai/RAG/NestJS/retrieval.service';
import { MealPlanningAgent } from '../../../orchestration/NestJS/meal-planning.agent';
import { OrchestratorService } from '../../../orchestration/NestJS/orchestrator.service';

describe('AssistantService', () => {
  it('creates a conversation and persists messages', async () => {
    const ollamaClient = {
      chat: jest.fn().mockResolvedValue({ content: 'assistant response', toolCalls: [] }),
    } as unknown as jest.Mocked<OllamaClient>;
    const conversationStore = new FakeConversationStore();
    const toolExecutor = createToolExecutor();
    const service = createService(ollamaClient, conversationStore, toolExecutor);

    const result = await service.chat('Hello');

    expect(result).toEqual({ conversationId: 'conversation-1', response: 'assistant response' });
    expect(ollamaClient.chat).toHaveBeenCalledWith(undefined, [
      expect.objectContaining({ role: 'System', content: 'system prompt' }),
      expect.objectContaining({ role: 'User', content: 'Hello' }),
    ], expect.any(Array));
    expect(conversationStore.messages.map((message) => message.content)).toEqual(['Hello', 'assistant response']);
  });

  it('includes previous conversation history', async () => {
    const ollamaClient = {
      chat: jest.fn().mockResolvedValue({ content: 'second response', toolCalls: [] }),
    } as unknown as jest.Mocked<OllamaClient>;
    const conversationStore = new FakeConversationStore([
      { role: 'User', content: 'First', timestamp: new Date() },
      { role: 'Assistant', content: 'First response', timestamp: new Date() },
    ]);
    const service = createService(ollamaClient, conversationStore, createToolExecutor());

    const result = await service.chat('Second', 'conversation-1');

    expect(result.conversationId).toBe('conversation-1');
    expect(ollamaClient.chat.mock.calls[0][1].map((message) => message.content)).toEqual([
      'system prompt',
      'First',
      'First response',
      'Second',
    ]);
  });

  it('executes one tool call and returns the final response', async () => {
    const ollamaClient = {
      chat: jest.fn()
        .mockResolvedValueOnce({ content: '', toolCalls: [{ name: 'example_tool', arguments: { message: 'hello' } }] })
        .mockResolvedValueOnce({ content: 'done', toolCalls: [] }),
    } as unknown as jest.Mocked<OllamaClient>;
    const toolExecutor = createToolExecutor();
    const conversationStore = new FakeConversationStore();
    const service = createService(ollamaClient, conversationStore, toolExecutor);

    const result = await service.chat('Use tool');

    expect(result.response).toBe('done');
    expect(toolExecutor.execute).toHaveBeenCalledWith('example_tool', { message: 'hello' });
    expect(conversationStore.messages.some((message) => message.role === 'Tool')).toBe(true);
  });

  it('uses RAG for repository questions after no tool call and includes sources', async () => {
    const ollamaClient = {
      chat: jest.fn()
        .mockResolvedValueOnce({ content: 'direct draft', toolCalls: [] })
        .mockResolvedValueOnce({ content: 'Use the spicy chicken recipe.', toolCalls: [] }),
    } as unknown as jest.Mocked<OllamaClient>;
    const retrievalService = {
      retrieve: jest.fn().mockResolvedValue({
        question: 'What spicy chicken meal can I cook?',
        recipes: [{ recipeId: '1', title: 'Spicy Chicken', description: 'Dinner', tags: 'spicy', ingredients: ['chicken'], preparationSteps: 'Slice', cookingTime: '45', similarityScore: 0.91 }],
        sources: [{ recipeId: '1', title: 'Spicy Chicken', similarityScore: 0.91 }],
      }),
    } as unknown as jest.Mocked<RetrievalService>;
    const promptBuilder = {
      build: jest.fn().mockReturnValue('rag prompt'),
    } as unknown as jest.Mocked<PromptBuilder>;
    const service = createService(ollamaClient, new FakeConversationStore(), createToolExecutor(), createOptions(), retrievalService, promptBuilder);

    const result = await service.chat('What spicy chicken meal can I cook?');

    expect(result.response).toContain('Use the spicy chicken recipe.');
    expect(result.response).toContain('Sources:');
    expect(result.response).toContain('1: Spicy Chicken');
    expect(ollamaClient.chat).toHaveBeenCalledTimes(2);
    expect(ollamaClient.chat.mock.calls[1][2]).toEqual([]);
  });

  it('returns a no-repository-information message for empty retrieval', async () => {
    const ollamaClient = {
      chat: jest.fn().mockResolvedValue({ content: 'direct draft', toolCalls: [] }),
    } as unknown as jest.Mocked<OllamaClient>;
    const retrievalService = {
      retrieve: jest.fn().mockResolvedValue({ question: 'unknown', recipes: [], sources: [] }),
    } as unknown as jest.Mocked<RetrievalService>;
    const service = createService(ollamaClient, new FakeConversationStore(), createToolExecutor(), createOptions(), retrievalService, { build: jest.fn() } as unknown as PromptBuilder);

    const result = await service.chat('What freezer meal uses moon dust?');

    expect(result.response).toContain('repository does not contain enough information');
    expect(result.response).toContain('Sources: none');
  });

  it('executes multiple sequential tool calls', async () => {
    const ollamaClient = {
      chat: jest.fn()
        .mockResolvedValueOnce({ content: '', toolCalls: [{ name: 'example_tool', arguments: {} }] })
        .mockResolvedValueOnce({ content: '', toolCalls: [{ name: 'second_tool', arguments: {} }] })
        .mockResolvedValueOnce({ content: 'complete', toolCalls: [] }),
    } as unknown as jest.Mocked<OllamaClient>;
    const toolExecutor = createToolExecutor();
    const service = createService(ollamaClient, new FakeConversationStore(), toolExecutor);

    const result = await service.chat('Use tools');

    expect(result.response).toBe('complete');
    expect(toolExecutor.execute).toHaveBeenCalledTimes(2);
  });

  it('appends tool failures and returns the final response', async () => {
    const ollamaClient = {
      chat: jest.fn()
        .mockResolvedValueOnce({ content: '', toolCalls: [{ name: 'example_tool', arguments: {} }] })
        .mockResolvedValueOnce({ content: 'tool failed gracefully', toolCalls: [] }),
    } as unknown as jest.Mocked<OllamaClient>;
    const toolExecutor = createToolExecutor({ success: false });
    const conversationStore = new FakeConversationStore();
    const service = createService(ollamaClient, conversationStore, toolExecutor);

    const result = await service.chat('Use failing tool');

    expect(result.response).toBe('tool failed gracefully');
    expect(conversationStore.messages.find((message) => message.role === 'Tool')?.content).toContain('failed');
  });

  it('handles invalid tools without repository access', async () => {
    const ollamaClient = {
      chat: jest.fn()
        .mockResolvedValueOnce({ content: '', toolCalls: [{ name: 'missing_tool', arguments: {} }] })
        .mockResolvedValueOnce({ content: 'invalid tool handled', toolCalls: [] }),
    } as unknown as jest.Mocked<OllamaClient>;
    const toolExecutor = createToolExecutor();
    toolExecutor.execute.mockRejectedValueOnce(new Error('Unknown tool: missing_tool'));
    const service = createService(ollamaClient, new FakeConversationStore(), toolExecutor);

    const result = await service.chat('Use missing tool');

    expect(result.response).toBe('invalid tool handled');
  });

  it('returns a graceful error when the tool-call limit is exceeded', async () => {
    const ollamaClient = {
      chat: jest.fn().mockResolvedValue({
        content: '',
        toolCalls: [
          { name: 'example_tool', arguments: {} },
          { name: 'second_tool', arguments: {} },
        ],
      }),
    } as unknown as jest.Mocked<OllamaClient>;
    const toolExecutor = createToolExecutor();
    const service = createService(ollamaClient, new FakeConversationStore(), toolExecutor, {
      ...createOptions(),
      maximumToolCallsPerRequest: 1,
    });

    const result = await service.chat('Loop');

    expect(result.response).toContain('maximum tool call limit');
    expect(toolExecutor.execute).toHaveBeenCalledTimes(1);
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

function createOptions() {
  return {
    systemPrompt: 'system prompt',
    maximumToolCallsPerRequest: 10,
    maximumConversationSize: 100,
    maximumExecutionTimeMs: 120000,
  };
}

function createService(
  ollamaClient: jest.Mocked<OllamaClient>,
  conversationStore: ConversationStore,
  toolExecutor: jest.Mocked<ToolExecutor>,
  options = createOptions(),
  retrievalService?: jest.Mocked<RetrievalService>,
  promptBuilder?: jest.Mocked<PromptBuilder> | PromptBuilder
) {
  const agent = new MealPlanningAgent(ollamaClient, toolExecutor, retrievalService, promptBuilder);
  const orchestrator = new OrchestratorService([agent]);
  return new AssistantService(conversationStore, orchestrator, options);
}

function createToolExecutor(result = { success: true }) {
  return {
    getTools: jest.fn().mockReturnValue([
      { name: 'example_tool', description: 'Example tool' },
      { name: 'second_tool', description: 'Second tool' },
    ]),
    execute: jest.fn().mockImplementation((tool) => Promise.resolve({
      success: result.success,
      tool,
      output: result.success ? { ok: true } : undefined,
      error: result.success ? undefined : 'failed',
    })),
  } as unknown as jest.Mocked<ToolExecutor>;
}