import { ConversationMessage } from './conversation-store';
import { OllamaChatResult } from './ollama-chat-result';
import { ToolDefinition } from './tool-registry';

export interface OllamaClientInterface {
  chat(model: string | undefined, messages: ConversationMessage[], tools: ToolDefinition[]): Promise<OllamaChatResult>;
}