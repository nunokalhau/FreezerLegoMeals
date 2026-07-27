import { ChromaVectorStore } from '../../../ai/VectorStores/NestJS/chroma-vector-store';
import { ChromaVectorStoreOptions } from '../../../ai/VectorStores/NestJS/chroma-vector-store-options';
import { cosineSimilarity } from '../../../ai/VectorStores/NestJS/vector-store';
import { ISemanticRecipeMetadataProvider, SemanticSearchService } from '../../../ai/SemanticSearch/NestJS/semantic-search.service';

const chromaOptions: ChromaVectorStoreOptions = {
  baseUrl: 'http://localhost:8001',
  tenant: 'default_tenant',
  database: 'default_database',
  collectionName: 'recipe_embeddings',
  timeoutMs: 5000,
};

describe('SemanticSearchService', () => {
  it('calculates cosine similarity', () => {
    expect(cosineSimilarity([1, 0], [1, 0])).toBe(1);
    expect(cosineSimilarity([1, 0], [0, 1])).toBe(0);
    expect(cosineSimilarity([1, 0], [])).toBe(0);
  });

  it('ChromaVectorStore ranks topK and caches collection id', async () => {
    const fetchMock = jest.fn()
      .mockResolvedValueOnce(response(200, {
        id: 'collection-1',
        name: 'recipe_embeddings',
      }))
      .mockResolvedValueOnce(response(200, {
        ids: [['1']],
        embeddings: [[[1, 0]]],
        distances: [[0]],
        include: ['embeddings', 'distances'],
      }))
      .mockResolvedValueOnce(response(200, {
        ids: [['1', '2']],
        embeddings: [[[1, 0], [0, 1]]],
        distances: [[0, 1]],
        include: ['embeddings', 'distances'],
      }));

    const originalFetch = global.fetch;
    (global as any).fetch = fetchMock;
    try {
      const store = new ChromaVectorStore(chromaOptions);
      const first = await store.search([1, 0], 1);
      const second = await store.search([1, 0], 2);

      expect(first.map((match) => match.recipeId)).toEqual(['1']);
      expect(second.map((match) => match.recipeId)).toEqual(['1', '2']);
      expect(fetchMock).toHaveBeenCalledTimes(3);
      const firstBody = JSON.parse(fetchMock.mock.calls[0][1].body as string);
      expect(firstBody).toEqual({ name: 'recipe_embeddings', get_or_create: true });
    } finally {
      (global as any).fetch = originalFetch;
    }
  });

  it('ChromaVectorStore returns empty results for an empty index', async () => {
    const fetchMock = jest.fn()
      .mockResolvedValueOnce(response(200, { id: 'collection-1' }))
      .mockResolvedValueOnce(response(200, {
        ids: [[]],
        embeddings: [[]],
        distances: [[]],
        include: ['embeddings', 'distances'],
      }));

    const originalFetch = global.fetch;
    (global as any).fetch = fetchMock;
    try {
      await expect(new ChromaVectorStore(chromaOptions).search([1, 0], 5)).resolves.toEqual([]);
    } finally {
      (global as any).fetch = originalFetch;
    }
  });

  it('ChromaVectorStore with missing collection name throws', () => {
    expect(() => new ChromaVectorStore({ ...chromaOptions, collectionName: ' ' })).toThrow('collection name');
  });

  it('ChromaVectorStore throws when collection creation response has no id', async () => {
    const fetchMock = jest.fn().mockResolvedValueOnce(response(200, { name: 'recipe_embeddings' }));
    const originalFetch = global.fetch;
    (global as any).fetch = fetchMock;
    try {
      await expect(new ChromaVectorStore(chromaOptions).search([1, 0], 1)).rejects.toThrow('did not include an id');
    } finally {
      (global as any).fetch = originalFetch;
    }
  });

  it('ChromaVectorStore uses distance fallback when embeddings are missing', async () => {
    const fetchMock = jest.fn()
      .mockResolvedValueOnce(response(200, { id: 'collection-1' }))
      .mockResolvedValueOnce(response(200, {
        ids: [['2', '1']],
        embeddings: null,
        distances: [[0.6, 0.1]],
        include: ['distances'],
      }));

    const originalFetch = global.fetch;
    (global as any).fetch = fetchMock;
    try {
      const matches = await new ChromaVectorStore(chromaOptions).search([1, 0], 2);
      expect(matches.map((match) => match.recipeId)).toEqual(['1', '2']);
      expect(matches[0].score).toBeCloseTo(0.9, 5);
      expect(matches[1].score).toBeCloseTo(0.4, 5);
    } finally {
      (global as any).fetch = originalFetch;
    }
  });

  it('returns rich semantic search results', async () => {
    const service = new SemanticSearchService(
      { generateEmbedding: jest.fn().mockResolvedValue({ model: 'test', dimensions: 2, embedding: [1, 0] }) },
      { search: jest.fn().mockResolvedValue([{ recipeId: '1', score: 1 }]) },
      new StubMetadataProvider()
    );

    const results = await service.search('spicy dinner', 1);

    expect(results).toEqual([{ recipeId: '1', title: 'Spicy Chicken', score: 1, matchedText: 'spicy chicken dinner', reason: 'High semantic similarity between the query and Spicy Chicken.' }]);
  });

  it('returns empty results for blank queries or invalid topK', async () => {
    const service = new SemanticSearchService(
      { generateEmbedding: jest.fn() },
      { search: jest.fn() },
      new StubMetadataProvider()
    );

    await expect(service.search(' ', 5)).resolves.toEqual([]);
    await expect(service.search('anything', 0)).resolves.toEqual([]);
  });
});

class StubMetadataProvider implements ISemanticRecipeMetadataProvider {
  async getMetadata(recipeId: string) {
    return { recipeId, title: 'Spicy Chicken', matchedText: 'spicy chicken dinner' };
  }
}

function response(status: number, body: unknown): Response {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: async () => body,
  } as Response;
}