import { OrchestratorContext } from './orchestrator-context';
import { OrchestratorResult } from './orchestrator-result';

export interface Agent {
  readonly name: string;
  canHandle(context: OrchestratorContext): boolean;
  execute(context: OrchestratorContext): Promise<OrchestratorResult>;
}