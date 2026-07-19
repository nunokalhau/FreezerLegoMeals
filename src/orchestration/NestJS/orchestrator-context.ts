import { AssistantOptions } from '../../services/Services.NestJS/assistant-options';
import { ConversationMessage } from '../../services/Services.NestJS/conversation-store';

export interface OrchestratorContext {
  userRequest: string;
  currentTimestamp: Date;
  correlationId: string;
  metadata: Record<string, unknown>;
  conversationId: string;
  messages: ConversationMessage[];
  messagesToPersist: ConversationMessage[];
  assistantOptions: AssistantOptions;
  // TODO: Add Conversation Memory references when that phase starts.
  // TODO: Add Redis-backed orchestration state when distributed execution is introduced.
}