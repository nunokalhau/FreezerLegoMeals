import { Agent } from './agent.interface';
import { OrchestratorContext } from './orchestrator-context';
import { OrchestratorResult } from './orchestrator-result';

export class NutritionAgent implements Agent {
  readonly name = 'NutritionAgent';

  canHandle(_context: OrchestratorContext): boolean {
    return false;
  }

  async execute(context: OrchestratorContext): Promise<OrchestratorResult> {
    return {
      finalResponse: 'NutritionAgent is not active yet.',
      selectedAgent: this.name,
      executedTools: [],
      retrievedRecipes: [],
      executionSteps: ['Assistant', 'Orchestrator', this.name],
      executionDurationMs: 0,
      errors: ['Agent is not active.'],
      messagesToPersist: context.messagesToPersist,
    };
  }
}