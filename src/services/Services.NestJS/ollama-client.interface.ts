import { ConversationMessage } from './conversation-store';

export interface OllamaClientInterface {
  chat(model: string | undefined, messages: ConversationMessage[]): Promise<string>;
}