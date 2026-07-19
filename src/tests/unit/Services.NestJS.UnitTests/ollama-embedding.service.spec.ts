import { OllamaEmbeddingService } from '../../../ai/Embedding.NestJS/ollama-embedding.service';

describe('OllamaEmbeddingService', () => {
  const originalFetch = global.fetch;

  afterEach(() => {
    global.fetch = originalFetch;
    jest.restoreAllMocks();
  });

  it('posts to Ollama and returns an embedding vector', async () => {
    global.fetch = jest.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ embedding: [0.1, 0.2, 0.3] }),
    } as Response);
    const service = new OllamaEmbeddingService({
      ollamaBaseUrl: 'http://localhost:11434',
      model: 'test-model',
      timeoutMs: 1000,
    });

    const response = await service.generateEmbedding('Chicken curry');

    expect(response).toEqual({ model: 'test-model', dimensions: 3, embedding: [0.1, 0.2, 0.3] });
    expect(global.fetch).toHaveBeenCalledWith(
      new URL('/api/embeddings', 'http://localhost:11434'),
      expect.objectContaining({ method: 'POST' })
    );
  });

  it('rejects blank text', async () => {
    const service = new OllamaEmbeddingService({
      ollamaBaseUrl: 'http://localhost:11434',
      model: 'test-model',
      timeoutMs: 1000,
    });

    await expect(service.generateEmbedding(' ')).rejects.toThrow('Text is required');
  });
});