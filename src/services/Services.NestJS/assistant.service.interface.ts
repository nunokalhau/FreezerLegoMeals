export interface AssistantServiceInterface {
  chat(message: string): Promise<string>;
}