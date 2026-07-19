import { OrchestratorContext } from './orchestrator-context';
import { OrchestratorResult } from './orchestrator-result';

export interface AssistantOrchestratorInterface {
  execute(context: OrchestratorContext): Promise<OrchestratorResult>;
}