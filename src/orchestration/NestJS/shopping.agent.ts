import { Agent } from './agent.interface';
import { OrchestratorContext } from './orchestrator-context';
import { OrchestratorResult } from './orchestrator-result';

export class ShoppingAgent implements Agent {
  readonly name = 'ShoppingAgent';

  canHandle(_context: OrchestratorContext): boolean {
    return false;
  }

  async execute(context: OrchestratorContext): Promise<OrchestratorResult> {
    return {
      finalResponse: 'ShoppingAgent is not active yet.',
      selectedAgent: this.name,
      executedTools: [],
      retrievedRecipes: [],
      executionSteps: ['Assistant', 'AssistantOrchestrator', this.name],
      executionDurationMs: 0,
      errors: ['Agent is not active.'],
      messagesToPersist: context.messagesToPersist,
    };
  }
}