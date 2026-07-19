import { Test, TestingModule } from '@nestjs/testing';
import { INestApplication } from '@nestjs/common';
import { execFileSync } from 'child_process';
import request from 'supertest';
import { AppModule } from '../../../api/WebApi.NestJS/app.module';

const OLLAMA_BASE_URL = 'http://localhost:11434';
const EMBEDDING_MODEL = 'nomic-embed-text';
const EMBEDDING_INTEGRATION_TIMEOUT_MS = 90000;

describe('Embedding Endpoint (End-to-End Integration)', () => {
  jest.setTimeout(EMBEDDING_INTEGRATION_TIMEOUT_MS);

  const availability = getOllamaEmbeddingAvailability();
  const embeddingIt = availability.isAvailable ? it : it.skip;
  const testName = availability.isAvailable
    ? 'POST /embeddings returns an embedding vector with local Ollama'
    : `POST /embeddings returns an embedding vector with local Ollama (${availability.skipReason})`;

  let app: INestApplication | undefined;
  let originalEmbeddingModel: string | undefined;

  beforeAll(async () => {
    originalEmbeddingModel = process.env.OLLAMA_EMBEDDING_MODEL;

    if (!availability.isAvailable) {
      return;
    }

    process.env.OLLAMA_BASE_URL = OLLAMA_BASE_URL;
    process.env.OLLAMA_EMBEDDING_MODEL = EMBEDDING_MODEL;
    process.env.OLLAMA_EMBEDDING_TIMEOUT_MS = '60000';

    const moduleFixture: TestingModule = await Test.createTestingModule({
      imports: [AppModule],
    }).compile();

    app = moduleFixture.createNestApplication();
    await app.init();
  });

  afterAll(async () => {
    if (app) {
      await app.close();
    }

    if (originalEmbeddingModel === undefined) {
      delete process.env.OLLAMA_EMBEDDING_MODEL;
    } else {
      process.env.OLLAMA_EMBEDDING_MODEL = originalEmbeddingModel;
    }
  });

  embeddingIt(testName, async () => {
    const response = await request(app.getHttpServer())
      .post('/embeddings')
      .send({ text: 'Chicken curry with rice' })
      .timeout({ response: 70000, deadline: EMBEDDING_INTEGRATION_TIMEOUT_MS })
      .expect(201);

    expect(response.body.model).toBe(EMBEDDING_MODEL);
    expect(response.body.dimensions).toBeGreaterThan(0);
    expect(response.body.embedding).toHaveLength(response.body.dimensions);
  }, EMBEDDING_INTEGRATION_TIMEOUT_MS);
});

function getOllamaEmbeddingAvailability(): { isAvailable: boolean; skipReason?: string } {
  try {
    const body = JSON.parse(execFileSync(process.execPath, [
      '-e',
      `const controller = new AbortController();
       const timeout = setTimeout(() => controller.abort(), 5000);
       fetch('${OLLAMA_BASE_URL}/api/tags', { signal: controller.signal })
         .then(async response => {
           if (!response.ok) {
             console.log(JSON.stringify({ isAvailable: false, skipReason: 'GET /api/tags returned ' + response.status + '.' }));
             return;
           }

           const body = await response.json();
           console.log(JSON.stringify({ isAvailable: true, body }));
         })
         .catch(error => console.log(JSON.stringify({ isAvailable: false, skipReason: 'GET /api/tags failed or timed out: ' + error.message })))
         .finally(() => clearTimeout(timeout));`,
    ], { encoding: 'utf8', timeout: 7000 }));

    if (!body.isAvailable) {
      return body;
    }

    const names = new Set((body?.body?.models || []).map((candidate) => String(candidate?.name || '').split(':')[0]));
    if (!names.has(EMBEDDING_MODEL)) {
      return { isAvailable: false, skipReason: `Ollama model ${EMBEDDING_MODEL} is not installed.` };
    }

    return { isAvailable: true };
  } catch (error) {
    return {
      isAvailable: false,
      skipReason: `GET /api/tags failed or timed out: ${error instanceof Error ? error.message : String(error)}`,
    };
  }
}