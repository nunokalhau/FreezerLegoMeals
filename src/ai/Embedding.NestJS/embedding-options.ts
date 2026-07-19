export type EmbeddingOptions = {
  ollamaBaseUrl: string;
  model: string;
  timeoutMs: number;
};

export const EMBEDDING_OPTIONS = Symbol('EMBEDDING_OPTIONS');

export function createEmbeddingOptions(): EmbeddingOptions {
  return {
    ollamaBaseUrl: process.env.OLLAMA_BASE_URL || process.env.OLLAMA_EMBEDDING_BASE_URL || 'http://localhost:11434',
    model: process.env.OLLAMA_EMBEDDING_MODEL || 'nomic-embed-text',
    timeoutMs: Number(process.env.OLLAMA_EMBEDDING_TIMEOUT_MS || 60000),
  };
}