import { OrchestratorContext } from './orchestrator-context';
import { OrchestratorResult } from './orchestrator-result';

export interface OrchestratorInterface {
  execute(context: OrchestratorContext): Promise<OrchestratorResult>;
}