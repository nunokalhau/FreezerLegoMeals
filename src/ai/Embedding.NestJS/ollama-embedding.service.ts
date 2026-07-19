import { Inject, Injectable } from '@nestjs/common';
import { EMBEDDING_OPTIONS, EmbeddingOptions } from './embedding-options';
import { EmbeddingResponse, IEmbeddingService } from './embedding.service.interface';

@Injectable()
export class OllamaEmbeddingService implements IEmbeddingService {
  constructor(
    @Inject(EMBEDDING_OPTIONS)
    private readonly options: EmbeddingOptions
  ) {}

  async generateEmbedding(text: string): Promise<EmbeddingResponse> {
    if (!text || !text.trim()) {
      throw new Error('Text is required to generate an embedding');
    }

    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), this.options.timeoutMs);

    try {
      const response = await fetch(new URL('/api/embeddings', this.options.ollamaBaseUrl), {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ model: this.options.model, prompt: text }),
        signal: controller.signal,
      });

      if (!response.ok) {
        throw new Error(`Ollama embedding request failed with status ${response.status}`);
      }

      const data = await response.json();
      if (!Array.isArray(data?.embedding) || data.embedding.length === 0) {
        throw new Error('Ollama did not return an embedding vector');
      }

      const embedding = data.embedding.map((value: number) => Number(value));
      return {
        model: this.options.model,
        dimensions: embedding.length,
        embedding,
      };
    } finally {
      clearTimeout(timeout);
    }
  }
}