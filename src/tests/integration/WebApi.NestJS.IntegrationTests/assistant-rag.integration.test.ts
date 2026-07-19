import { Test, TestingModule } from '@nestjs/testing';
import { INestApplication } from '@nestjs/common';
import request from 'supertest';
import { AppController } from '../../../api/WebApi.NestJS/app.controller';
import { AppService } from '../../../api/WebApi.NestJS/app.service';
import { AssistantService } from '../../../services/Services.NestJS/assistant.service';
import { MealService } from '../../../services/Services.NestJS/meal.service';
import { ShoppingService } from '../../../services/Services.NestJS/shopping.service';

describe('Assistant RAG Endpoint (Integration)', () => {
  let app: INestApplication;
  const assistantService = {
    chat: jest.fn().mockResolvedValue({
      conversationId: 'conversation-1',
      response: 'Use the spicy chicken recipe.\n\nSources:\n- 1: Spicy Chicken (similarityScore: 0.910000)',
    }),
  };

  beforeAll(async () => {
    const moduleFixture: TestingModule = await Test.createTestingModule({
      controllers: [AppController],
      providers: [
        AppService,
        { provide: AssistantService, useValue: assistantService },
        { provide: MealService, useValue: {} },
        { provide: ShoppingService, useValue: {} },
      ],
    }).compile();

    app = moduleFixture.createNestApplication();
    await app.init();
  });

  afterAll(async () => {
    await app.close();
  });

  it('POST /api/assistant/chat returns RAG answer without changing response shape', async () => {
    const response = await request(app.getHttpServer())
      .post('/api/assistant/chat')
      .set('Content-Type', 'application/json')
      .send(JSON.stringify({ message: 'What spicy chicken meal can I cook?' }))
      .expect(201);

    expect(Object.keys(response.body).sort()).toEqual(['conversationId', 'response']);
    expect(response.body.response).toContain('Use the spicy chicken recipe.');
    expect(response.body.response).toContain('Sources:');
    expect(response.body.response).toContain('1: Spicy Chicken');
    expect(assistantService.chat).toHaveBeenCalledWith('What spicy chicken meal can I cook?', undefined);
  });
});
