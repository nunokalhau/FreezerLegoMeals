import { promises as fs } from 'fs';
import { join } from 'path';

export type VectorMatch = {
  recipeId: string;
  score: number;
};

export abstract class IVectorStore {
  abstract search(queryEmbedding: number[], topK: number): Promise<VectorMatch[]>;
}

type StoredEmbedding = {
  recipeId: string;
  embedding: number[];
};

export class LocalVectorStore implements IVectorStore {
  private cache: StoredEmbedding[] | undefined;

  constructor(private readonly embeddingsDirectory: string) {}

  async search(queryEmbedding: number[], topK: number): Promise<VectorMatch[]> {
    if (topK <= 0) {
      return [];
    }

    const embeddings = await this.loadEmbeddingsOnce();
    return embeddings
      .map((embedding) => ({
        recipeId: embedding.recipeId,
        score: cosineSimilarity(queryEmbedding, embedding.embedding),
      }))
      .sort((left, right) => right.score - left.score)
      .slice(0, topK);
  }

  private async loadEmbeddingsOnce(): Promise<StoredEmbedding[]> {
    if (this.cache) {
      return this.cache;
    }

    try {
      const files = (await fs.readdir(this.embeddingsDirectory)).filter((file) => file.endsWith('.json')).sort();
      const embeddings: StoredEmbedding[] = [];
      for (const file of files) {
        const payload = JSON.parse(await fs.readFile(join(this.embeddingsDirectory, file), 'utf8'));
        if (Array.isArray(payload.embedding) && payload.embedding.length > 0) {
          embeddings.push({
            recipeId: String(payload.recipeId || file.replace(/\.json$/i, '')),
            embedding: payload.embedding.map((value: number) => Number(value)),
          });
        }
      }

      // TODO: Move this cache to Redis if local process memory becomes insufficient.
      this.cache = embeddings;
      return this.cache;
    } catch (error) {
      if ((error as NodeJS.ErrnoException).code === 'ENOENT') {
        this.cache = [];
        return this.cache;
      }

      throw error;
    }
  }
}

export function cosineSimilarity(left: number[], right: number[]): number {
  if (!left.length || !right.length || left.length !== right.length) {
    return 0;
  }

  let dot = 0;
  let leftNorm = 0;
  let rightNorm = 0;
  for (let index = 0; index < left.length; index++) {
    dot += left[index] * right[index];
    leftNorm += left[index] * left[index];
    rightNorm += right[index] * right[index];
  }

  if (leftNorm === 0 || rightNorm === 0) {
    return 0;
  }

  return dot / (Math.sqrt(leftNorm) * Math.sqrt(rightNorm));
}