import { Test, TestingModule } from '@nestjs/testing';
import { INestApplication } from '@nestjs/common';
import { execFileSync } from 'child_process';
import request from 'supertest';
import { IEmbeddingService } from '../../../ai/Embedding.NestJS/embedding.service.interface';
import { AppModule } from '../../../api/WebApi.NestJS/app.module';

const CHROMA_BASE_URL = 'http://localhost:8001';
const TENANT = 'default_tenant';
const DATABASE = 'default_database';
const CHROMA_REQUIRED_MESSAGE = `Local ChromaDB is required at ${CHROMA_BASE_URL}.`;
const SEMANTIC_CHROMA_TIMEOUT_MS = 90000;

describe('Semantic Search Endpoint (Chroma Integration)', () => {
  jest.setTimeout(SEMANTIC_CHROMA_TIMEOUT_MS);

  const availability = getChromaAvailability();
  const semanticIt = availability.isAvailable ? it : it.skip;
  const testName = availability.isAvailable
    ? 'POST /api/semantic-search uses ChromaDB collection create/reuse and ranked query results'
    : `POST /api/semantic-search uses ChromaDB collection create/reuse and ranked query results (${availability.skipReason})`;

  let app: INestApplication | undefined;
  let collectionName = '';
  let collectionId: string | undefined;
  let originalCollectionName: string | undefined;

  beforeAll(async () => {
    originalCollectionName = process.env.CHROMA_VECTOR_STORE_COLLECTION_NAME;

    if (!availability.isAvailable) {
      return;
    }

    collectionName = `itest_recipe_embeddings_${Math.random().toString(16).slice(2)}`;
    process.env.CHROMA_VECTOR_STORE_BASE_URL = CHROMA_BASE_URL;
    process.env.CHROMA_VECTOR_STORE_TENANT = TENANT;
    process.env.CHROMA_VECTOR_STORE_DATABASE = DATABASE;
    process.env.CHROMA_VECTOR_STORE_COLLECTION_NAME = collectionName;
    process.env.CHROMA_VECTOR_STORE_TIMEOUT_MS = '15000';

    const moduleFixture: TestingModule = await Test.createTestingModule({
      imports: [AppModule],
    })
      .overrideProvider(IEmbeddingService)
      .useValue({
        generateEmbedding: jest.fn().mockResolvedValue({
          model: 'test',
          dimensions: 2,
          embedding: [1, 0],
        }),
      })
      .compile();

    app = moduleFixture.createNestApplication();
    await app.init();
  });

  afterAll(async () => {
    if (collectionId) {
      await deleteCollection(collectionId);
    }

    if (app) {
      await app.close();
    }

    if (originalCollectionName === undefined) {
      delete process.env.CHROMA_VECTOR_STORE_COLLECTION_NAME;
    } else {
      process.env.CHROMA_VECTOR_STORE_COLLECTION_NAME = originalCollectionName;
    }
  });

  semanticIt(testName, async () => {
    const initial = await request(app.getHttpServer())
      .post('/api/semantic-search')
      .set('Content-Type', 'application/json')
      .send(JSON.stringify({ query: 'spicy', topK: 2 }))
      .expect(201);

    expect(initial.body).toEqual([]);

    collectionId = await getCollectionIdByName(collectionName);
    expect(collectionId).toBeTruthy();

    const recipeIds = await getExistingRecipeIds(app, 2);
    if (recipeIds.length < 2) {
      return;
    }

    await upsertEmbeddings(collectionId!, [String(recipeIds[0]), String(recipeIds[1])], [[1, 0], [0, 1]]);

    const firstResults = await request(app.getHttpServer())
      .post('/api/semantic-search')
      .set('Content-Type', 'application/json')
      .send(JSON.stringify({ query: 'spicy', topK: 2 }))
      .expect(201);

    expect(firstResults.body.map((item: { recipeId: string }) => item.recipeId)).toEqual([String(recipeIds[0]), String(recipeIds[1])]);
    expect(firstResults.body[0].score).toBeGreaterThanOrEqual(firstResults.body[1].score);

    const moduleFixture: TestingModule = await Test.createTestingModule({
      imports: [AppModule],
    })
      .overrideProvider(IEmbeddingService)
      .useValue({
        generateEmbedding: jest.fn().mockResolvedValue({
          model: 'test',
          dimensions: 2,
          embedding: [1, 0],
        }),
      })
      .compile();

    const secondApp = moduleFixture.createNestApplication();
    await secondApp.init();

    try {
      const secondResults = await request(secondApp.getHttpServer())
        .post('/api/semantic-search')
        .set('Content-Type', 'application/json')
        .send(JSON.stringify({ query: 'spicy', topK: 2 }))
        .expect(201);

      expect(secondResults.body.map((item: { recipeId: string }) => item.recipeId)).toEqual([String(recipeIds[0]), String(recipeIds[1])]);
    } finally {
      await secondApp.close();
    }
  }, SEMANTIC_CHROMA_TIMEOUT_MS);
});

async function getExistingRecipeIds(app: INestApplication, count: number): Promise<number[]> {
  const response = await request(app.getHttpServer())
    .get('/api/recipes')
    .expect(200);

  return (response.body || [])
    .map((recipe: { id?: number }) => recipe.id)
    .filter((id: number | undefined): id is number => typeof id === 'number' && id > 0)
    .slice(0, count);
}

async function getCollectionIdByName(collectionName: string): Promise<string | undefined> {
  const response = await fetch(
    `${CHROMA_BASE_URL}/api/v2/tenants/${encodeURIComponent(TENANT)}/databases/${encodeURIComponent(DATABASE)}/collections`
  );
  if (!response.ok) {
    throw new Error(`Unable to list Chroma collections: ${response.status}`);
  }

  const payload = await response.json() as Array<{ id?: string; name?: string }>;
  return payload.find((item) => item.name === collectionName)?.id;
}

async function upsertEmbeddings(collectionId: string, ids: string[], embeddings: number[][]): Promise<void> {
  const response = await fetch(
    `${CHROMA_BASE_URL}/api/v2/tenants/${encodeURIComponent(TENANT)}/databases/${encodeURIComponent(DATABASE)}/collections/${encodeURIComponent(collectionId)}/upsert`,
    {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ ids, embeddings }),
    }
  );

  if (!response.ok) {
    throw new Error(`Unable to upsert Chroma embeddings: ${response.status}`);
  }
}

async function deleteCollection(collectionId: string): Promise<void> {
  const deleteByIdResponse = await fetch(
    `${CHROMA_BASE_URL}/api/v2/tenants/${encodeURIComponent(TENANT)}/databases/${encodeURIComponent(DATABASE)}/collections/by-id/${encodeURIComponent(collectionId)}`,
    {
      method: 'DELETE',
    }
  );
  if (deleteByIdResponse.ok) {
    return;
  }

  await fetch(
    `${CHROMA_BASE_URL}/api/v2/tenants/${encodeURIComponent(TENANT)}/databases/${encodeURIComponent(DATABASE)}/collections/${encodeURIComponent(collectionId)}/delete`,
    {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ ids: [] }),
    }
  );
}

function getChromaAvailability(): { isAvailable: boolean; skipReason?: string } {
  try {
    const body = JSON.parse(execFileSync(process.execPath, [
      '-e',
      `const controller = new AbortController();
       const timeout = setTimeout(() => controller.abort(), 5000);
       fetch('${CHROMA_BASE_URL}/api/v2/heartbeat', { signal: controller.signal })
         .then(async response => {
           if (!response.ok) {
             console.log(JSON.stringify({ isAvailable: false, skipReason: '${CHROMA_REQUIRED_MESSAGE} /api/v2/heartbeat returned ' + response.status + '.' }));
             return;
           }

           console.log(JSON.stringify({ isAvailable: true }));
         })
         .catch(error => console.log(JSON.stringify({ isAvailable: false, skipReason: '${CHROMA_REQUIRED_MESSAGE} heartbeat failed: ' + error.message })))
         .finally(() => clearTimeout(timeout));`,
    ], { encoding: 'utf8', timeout: 7000 }));

    return body;
  } catch (error) {
    return {
      isAvailable: false,
      skipReason: `${CHROMA_REQUIRED_MESSAGE} heartbeat failed: ${error instanceof Error ? error.message : String(error)}`,
    };
  }
}
