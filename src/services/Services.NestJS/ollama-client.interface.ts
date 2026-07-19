export interface OllamaClientInterface {
  chat(model: string | undefined, userMessage: string): Promise<string>;
}