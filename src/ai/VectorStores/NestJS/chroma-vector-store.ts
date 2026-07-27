import { ChromaVectorStoreOptions } from './chroma-vector-store-options';
import { cosineSimilarity, IVectorStore, VectorMatch } from './vector-store';

type QueryResponse = {
  ids?: string[][];
  embeddings?: Array<Array<number[] | null>> | null;
  distances?: Array<Array<number | null>> | null;
};

export class ChromaVectorStore implements IVectorStore {
  private collectionId: string | undefined;
  private collectionIdPromise: Promise<string> | undefined;

  constructor(private readonly options: ChromaVectorStoreOptions) {
    if (!options.collectionName?.trim()) {
      throw new Error('ChromaVectorStore collection name must be configured.');
    }

    if (!options.tenant?.trim()) {
      throw new Error('ChromaVectorStore tenant must be configured.');
    }

    if (!options.database?.trim()) {
      throw new Error('ChromaVectorStore database must be configured.');
    }
  }

  async search(queryEmbedding: number[], topK: number): Promise<VectorMatch[]> {
    if (topK <= 0 || queryEmbedding.length === 0) {
      return [];
    }

    const collectionId = await this.ensureCollectionId();
    const queryResponse = await this.postJson<QueryResponse>(this.buildCollectionQueryPath(collectionId), {
      query_embeddings: [queryEmbedding],
      n_results: topK,
      include: ['embeddings', 'distances'],
    });

    if (!queryResponse.ids || queryResponse.ids.length === 0) {
      return [];
    }

    const ids = queryResponse.ids[0] || [];
    if (ids.length === 0) {
      return [];
    }

    const embeddings = queryResponse.embeddings && queryResponse.embeddings.length > 0
      ? queryResponse.embeddings[0]
      : undefined;
    const distances = queryResponse.distances && queryResponse.distances.length > 0
      ? queryResponse.distances[0]
      : undefined;

    const matches: VectorMatch[] = [];
    for (let index = 0; index < ids.length; index++) {
      let score = 0;

      // Preserve LocalVectorStore behavior by preferring cosine similarity when vectors are available.
      const candidateEmbedding = embeddings?.[index];
      if (candidateEmbedding && candidateEmbedding.length > 0) {
        score = cosineSimilarity(queryEmbedding, candidateEmbedding.map((value) => Number(value)));
      } else {
        const distance = distances?.[index];
        if (typeof distance === 'number') {
          score = 1 - distance;
        }
      }

      matches.push({ recipeId: String(ids[index]), score });
    }

    return matches
      .sort((left, right) => right.score - left.score)
      .slice(0, topK);
  }

  private async ensureCollectionId(): Promise<string> {
    if (this.collectionId) {
      return this.collectionId;
    }

    if (!this.collectionIdPromise) {
      this.collectionIdPromise = this.createOrGetCollectionId()
        .then((collectionId) => {
          this.collectionId = collectionId;
          return collectionId;
        })
        .finally(() => {
          this.collectionIdPromise = undefined;
        });
    }

    return this.collectionIdPromise;
  }

  private async createOrGetCollectionId(): Promise<string> {
    const payload = {
      name: this.options.collectionName,
      get_or_create: true,
    };
    const response = await this.postJson<{ id?: string }>(this.buildCollectionsPath(), payload);

    if (!response.id || !response.id.trim()) {
      throw new Error('ChromaDB collection creation response did not include an id.');
    }

    return response.id;
  }

  private buildCollectionsPath(): string {
    return `/api/v2/tenants/${escape(this.options.tenant)}/databases/${escape(this.options.database)}/collections`;
  }

  private buildCollectionQueryPath(collectionId: string): string {
    return `${this.buildCollectionsPath()}/${escape(collectionId)}/query`;
  }

  private async postJson<TResponse>(path: string, payload: unknown): Promise<TResponse> {
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), this.options.timeoutMs);

    try {
      const response = await fetch(new URL(path, this.options.baseUrl), {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(payload),
        signal: controller.signal,
      });
      if (!response.ok) {
        throw new Error(`Chroma request failed with status ${response.status}`);
      }

      return await response.json() as TResponse;
    } finally {
      clearTimeout(timeout);
    }
  }
}

function escape(value: string): string {
  return encodeURIComponent(value);
}