import { PromptBuilder } from '../../../ai/RAG/NestJS/prompt-builder';
import { RetrievalService } from '../../../ai/RAG/NestJS/retrieval.service';

const metadataProvider = {
  getMetadata: jest.fn().mockResolvedValue({
    recipeId: '1',
    title: 'Spicy Chicken',
    matchedText: 'spicy chicken dinner',
    description: 'Freezer-friendly chicken dinner',
    tags: 'spicy, chicken',
    ingredients: ['chicken', 'pepper'],
    preparationSteps: 'Slice chicken and season it',
    cookingTime: '45',
  }),
};

describe('RAG services', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('retrieves structured context and sources', async () => {
    const semanticSearchService = {
      search: jest.fn().mockResolvedValue([{ recipeId: '1', title: 'Spicy Chicken', score: 0.91, matchedText: '', reason: '' }]),
    };
    const service = new RetrievalService(semanticSearchService as any, metadataProvider as any);

    const result = await service.retrieve('What spicy chicken meal can I cook?');

    expect(result.recipes).toHaveLength(1);
    expect(result.recipes[0].ingredients).toEqual(['chicken', 'pepper']);
    expect(result.recipes[0].preparationSteps).toBe('Slice chicken and season it');
    expect(result.sources).toEqual([{ recipeId: '1', title: 'Spicy Chicken', similarityScore: 0.91 }]);
  });

  it('filters low similarity matches', async () => {
    const semanticSearchService = {
      search: jest.fn().mockResolvedValue([{ recipeId: '1', title: 'Spicy Chicken', score: 0.01, matchedText: '', reason: '' }]),
    };
    const service = new RetrievalService(semanticSearchService as any, metadataProvider as any, 3, 0.2);

    const result = await service.retrieve('Unknown question');

    expect(result.recipes).toEqual([]);
    expect(result.sources).toEqual([]);
  });

  it('renders repository context into the prompt', () => {
    const builder = new PromptBuilder('Context:\n{recipes}\nQuestion:\n{question}');

    const prompt = builder.build('What can I cook?', [{
      recipeId: '1',
      title: 'Spicy Chicken',
      description: 'Dinner',
      tags: 'spicy',
      ingredients: ['chicken'],
      preparationSteps: 'Slice',
      cookingTime: '45',
      similarityScore: 0.91,
    }]);

    expect(prompt).toContain('Recipe ID: 1');
    expect(prompt).toContain('Ingredients: chicken');
    expect(prompt).toContain('Similarity score: 0.910000');
    expect(prompt).toContain('Question:\nWhat can I cook?');
  });
});
