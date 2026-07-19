export const ASSISTANT_OPTIONS = 'ASSISTANT_OPTIONS';

export interface AssistantOptions {
  systemPrompt: string;
  maximumToolCallsPerRequest: number;
  maximumConversationSize: number;
  maximumExecutionTimeMs: number;
}

export function createAssistantOptions(): AssistantOptions {
  return {
    systemPrompt: process.env.ASSISTANT_SYSTEM_PROMPT || 'You are a helpful meal planning assistant.',
    maximumToolCallsPerRequest: Number(process.env.ASSISTANT_MAXIMUM_TOOL_CALLS_PER_REQUEST || 10),
    maximumConversationSize: Number(process.env.ASSISTANT_MAXIMUM_CONVERSATION_SIZE || 100),
    maximumExecutionTimeMs: Number(process.env.ASSISTANT_MAXIMUM_EXECUTION_TIME_MS || 120000),
  };
}