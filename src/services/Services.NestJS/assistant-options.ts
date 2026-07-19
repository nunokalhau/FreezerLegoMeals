export const ASSISTANT_OPTIONS = 'ASSISTANT_OPTIONS';

export interface AssistantOptions {
  systemPrompt: string;
}

export function createAssistantOptions(): AssistantOptions {
  return {
    systemPrompt: process.env.ASSISTANT_SYSTEM_PROMPT || 'You are a helpful meal planning assistant.',
  };
}