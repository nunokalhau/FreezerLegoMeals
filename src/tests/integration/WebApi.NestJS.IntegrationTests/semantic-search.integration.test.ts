import { Test, TestingModule } from '@nestjs/testing';
import { INestApplication } from '@nestjs/common';
import request from 'supertest';
import { SemanticSearchController } from '../../../api/WebApi.NestJS/semantic-search.controller';
import { SemanticSearchService } from '../../../ai/SemanticSearch/NestJS/semantic-search.service';

describe('Semantic Search Endpoint (Integration)', () => {
  let app: INestApplication;
  const semanticSearchService = {
    search: jest.fn().mockResolvedValue([{ recipeId: '1', title: 'Spicy Chicken', score: 1, matchedText: 'spicy chicken', reason: 'High semantic similarity between the query and Spicy Chicken.' }]),
  };

  beforeAll(async () => {
    const moduleFixture: TestingModule = await Test.createTestingModule({
      controllers: [SemanticSearchController],
      providers: [{ provide: SemanticSearchService, useValue: semanticSearchService }],
    }).compile();

    app = moduleFixture.createNestApplication();
    await app.init();
  });

  afterAll(async () => {
    await app.close();
  });

  it('POST /api/semantic-search returns ranked semantic matches', async () => {
    const response = await request(app.getHttpServer())
      .post('/api/semantic-search')
      .send({ query: 'spicy', topK: 1 })
      .expect(201);

    expect(response.body).toHaveLength(1);
    expect(response.body[0].recipeId).toBe('1');
    expect(response.body[0].title).toBe('Spicy Chicken');
  });
});