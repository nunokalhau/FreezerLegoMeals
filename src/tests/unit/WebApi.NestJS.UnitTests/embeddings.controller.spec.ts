import { BadRequestException } from '@nestjs/common';
import { EmbeddingsController } from '../../../api/WebApi.NestJS/embeddings.controller';
import { IEmbeddingService } from '../../../ai/Embedding.NestJS/embedding.service.interface';

describe('EmbeddingsController', () => {
  it('returns embedding payload from the embedding service', async () => {
    const embeddingService: IEmbeddingService = {
      generateEmbedding: jest.fn().mockResolvedValue({ model: 'test-model', dimensions: 3, embedding: [0.1, 0.2, 0.3] }),
    };
    const controller = new EmbeddingsController(embeddingService);

    const response = await controller.generateEmbedding({ text: 'Chicken curry' });

    expect(response).toEqual({ model: 'test-model', dimensions: 3, embedding: [0.1, 0.2, 0.3] });
    expect(embeddingService.generateEmbedding).toHaveBeenCalledWith('Chicken curry');
  });

  it('rejects blank text', async () => {
    const controller = new EmbeddingsController({ generateEmbedding: jest.fn() });

    await expect(controller.generateEmbedding({ text: ' ' })).rejects.toBeInstanceOf(BadRequestException);
  });
});