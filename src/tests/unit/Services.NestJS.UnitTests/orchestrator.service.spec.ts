import { Agent } from '../../../orchestration/NestJS/agent.interface';
import { OrchestratorContext } from '../../../orchestration/NestJS/orchestrator-context';
import { OrchestratorService } from '../../../orchestration/NestJS/orchestrator.service';

describe('OrchestratorService', () => {
  it('selects the first agent that can handle the context', async () => {
    const skippedAgent = createAgent('SkippedAgent', false, 'skipped');
    const selectedAgent = createAgent('SelectedAgent', true, 'selected response');
    const orchestrator = new OrchestratorService([skippedAgent, selectedAgent]);

    const result = await orchestrator.execute(createContext());

    expect(result.finalResponse).toBe('selected response');
    expect(result.selectedAgent).toBe('SelectedAgent');
    expect(skippedAgent.execute).not.toHaveBeenCalled();
    expect(selectedAgent.execute).toHaveBeenCalledTimes(1);
  });

  it('returns an observable error when no agent can handle the context', async () => {
    const orchestrator = new OrchestratorService([createAgent('InactiveAgent', false, 'unused')]);

    const result = await orchestrator.execute(createContext());

    expect(result.selectedAgent).toBe('none');
    expect(result.finalResponse).toContain('No assistant agent');
    expect(result.errors).not.toHaveLength(0);
    expect(result.executionSteps).toContain('NoAgent');
  });
});

function createAgent(name: string, canHandle: boolean, response: string): jest.Mocked<Agent> {
  return {
    name,
    canHandle: jest.fn().mockReturnValue(canHandle),
    execute: jest.fn().mockImplementation((context: OrchestratorContext) => Promise.resolve({
      finalResponse: response,
      selectedAgent: name,
      executedTools: [],
      retrievedRecipes: [],
      executionSteps: ['Assistant', 'Orchestrator', name],
      executionDurationMs: 1,
      errors: [],
      messagesToPersist: context.messagesToPersist,
    })),
  };
}

function createContext(): OrchestratorContext {
  return {
    userRequest: 'Hello',
    currentTimestamp: new Date(),
    correlationId: 'correlation-1',
    metadata: {},
    conversationId: 'conversation-1',
    messages: [],
    messagesToPersist: [],
    assistantOptions: {
      systemPrompt: 'system prompt',
      maximumToolCallsPerRequest: 10,
      maximumConversationSize: 100,
      maximumExecutionTimeMs: 120000,
    },
  };
}