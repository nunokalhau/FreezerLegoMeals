import { mkdtempSync, rmSync, writeFileSync } from 'fs';
import { join } from 'path';
import { tmpdir } from 'os';
import { cosineSimilarity, LocalVectorStore } from '../../../ai/VectorStores/NestJS/local-vector-store';
import { ISemanticRecipeMetadataProvider, SemanticSearchService } from '../../../ai/SemanticSearch/NestJS/semantic-search.service';

describe('SemanticSearchService', () => {
  it('calculates cosine similarity', () => {
    expect(cosineSimilarity([1, 0], [1, 0])).toBe(1);
    expect(cosineSimilarity([1, 0], [0, 1])).toBe(0);
    expect(cosineSimilarity([1, 0], [])).toBe(0);
  });

  it('ranks topK results and caches embeddings', async () => {
    const root = mkdtempSync(join(tmpdir(), 'semantic-vectors-'));
    try {
      writeFileSync(join(root, '1.json'), JSON.stringify({ recipeId: '1', embedding: [1, 0] }));
      writeFileSync(join(root, '2.json'), JSON.stringify({ recipeId: '2', embedding: [0, 1] }));
      const store = new LocalVectorStore(root);

      const matches = await store.search([1, 0], 1);
      rmSync(join(root, '1.json'));
      const cachedMatches = await store.search([1, 0], 2);

      expect(matches.map((match) => match.recipeId)).toEqual(['1']);
      expect(cachedMatches.map((match) => match.recipeId)).toEqual(['1', '2']);
    } finally {
      rmSync(root, { recursive: true, force: true });
    }
  });

  it('returns empty results for an empty embedding index', async () => {
    const root = mkdtempSync(join(tmpdir(), 'semantic-vectors-'));
    try {
      await expect(new LocalVectorStore(root).search([1, 0], 5)).resolves.toEqual([]);
    } finally {
      rmSync(root, { recursive: true, force: true });
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