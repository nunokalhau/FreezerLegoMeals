export interface AssistantToolCall {
  name: string;
  arguments: Record<string, unknown>;
}

export interface OllamaChatResult {
  content: string;
  toolCalls: AssistantToolCall[];
}