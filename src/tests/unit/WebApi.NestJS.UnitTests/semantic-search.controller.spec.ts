import { BadRequestException } from '@nestjs/common';
import { SemanticSearchController } from '../../../api/WebApi.NestJS/semantic-search.controller';

describe('SemanticSearchController', () => {
  it('returns semantic search results', async () => {
    const service = { search: jest.fn().mockResolvedValue([{ recipeId: '1', title: 'Spicy Chicken', score: 1, matchedText: 'spicy chicken', reason: 'High semantic similarity between the query and Spicy Chicken.' }]) };
    const controller = new SemanticSearchController(service as any);

    const response = await controller.search({ query: 'spicy', topK: 1 });

    expect(response).toHaveLength(1);
    expect(service.search).toHaveBeenCalledWith('spicy', 1);
  });

  it('rejects blank query', async () => {
    const controller = new SemanticSearchController({ search: jest.fn() } as any);

    await expect(controller.search({ query: ' ' })).rejects.toBeInstanceOf(BadRequestException);
  });
});