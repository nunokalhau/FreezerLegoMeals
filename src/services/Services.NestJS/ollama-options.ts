export const OLLAMA_OPTIONS = 'OLLAMA_OPTIONS';

export interface OllamaOptions {
  baseUrl: string;
  defaultModel: string;
  timeoutMs: number;
}

export function createOllamaOptions(): OllamaOptions {
  return {
    baseUrl: process.env.OLLAMA_BASE_URL || 'http://localhost:11434',
    defaultModel: process.env.OLLAMA_DEFAULT_MODEL || 'llama3.2',
    timeoutMs: Number(process.env.OLLAMA_TIMEOUT_MS || 30000),
  };
}