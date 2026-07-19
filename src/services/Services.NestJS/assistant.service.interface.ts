export interface AssistantChatResult {
  conversationId: string;
  response: string;
}

export interface AssistantServiceInterface {
  chat(message: string, conversationId?: string): Promise<AssistantChatResult>;
}