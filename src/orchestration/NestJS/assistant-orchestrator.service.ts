import { Injectable, Logger } from '@nestjs/common';
import { Agent } from './agent.interface';
import { OrchestratorContext } from './orchestrator-context';
import { OrchestratorResult } from './orchestrator-result';

@Injectable()
export class AssistantOrchestratorService {
  private readonly logger = new Logger(AssistantOrchestratorService.name);

  constructor(private readonly agents: Agent[]) {}

  async execute(context: OrchestratorContext): Promise<OrchestratorResult> {
    this.logger.log(`AssistantOrchestrator started for correlation ${context.correlationId}`);
    const agent = this.agents.find((candidate) => candidate.canHandle(context));
    if (!agent) {
      const error = 'No assistant agent is available to handle that request.';
      this.logger.warn(error);
      return {
        finalResponse: error,
        selectedAgent: 'none',
        executedTools: [],
        retrievedRecipes: [],
        executionSteps: ['Assistant', 'AssistantOrchestrator', 'NoAgent'],
        executionDurationMs: 0,
        errors: [error],
        messagesToPersist: context.messagesToPersist,
      };
    }

    this.logger.log(`AssistantOrchestrator selected ${agent.name} for correlation ${context.correlationId}`);
    return agent.execute(context);
  }
}