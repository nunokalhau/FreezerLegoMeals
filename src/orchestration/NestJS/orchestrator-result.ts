import { ConversationMessage } from '../../services/Services.NestJS/conversation-store';

export interface RetrievedRecipeInfo {
  recipeId: string;
  title: string;
  similarityScore: number;
}

export interface OrchestratorResult {
  finalResponse: string;
  selectedAgent: string;
  executedTools: string[];
  retrievedRecipes: RetrievedRecipeInfo[];
  executionSteps: string[];
  executionDurationMs: number;
  errors: string[];
  messagesToPersist: ConversationMessage[];
}