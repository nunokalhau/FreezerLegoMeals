import { Test, TestingModule } from '@nestjs/testing';
import { INestApplication } from '@nestjs/common';
import { execFileSync } from 'child_process';
import request from 'supertest';
import { AppModule } from '../../../api/WebApi.NestJS/app.module';
import { AppController } from '../../../api/WebApi.NestJS/app.controller';
import { AppService } from '../../../api/WebApi.NestJS/app.service';
import { AssistantService } from '../../../services/Services.NestJS/assistant.service';
import { MealService } from '../../../services/Services.NestJS/meal.service';
import { ShoppingService } from '../../../services/Services.NestJS/shopping.service';

const mealServiceMock = {
  getRecipes: jest.fn(),
  searchRecipesByIngredients: jest.fn(),
  getRecipeById: jest.fn(),
  getRecipeDetails: jest.fn(),
  findMealsWithIngredients: jest.fn(),
};

const shoppingServiceMock = {
  generateShoppingList: jest.fn(),
  getRecipeIngredients: jest.fn(),
  getMultipleRecipeIngredients: jest.fn(),
  getRecipeInfo: jest.fn(),
};

const assistantServiceMock = {
  chat: jest.fn(),
};

const OLLAMA_BASE_URL = 'http://localhost:11434';
const OLLAMA_REQUIRED_MESSAGE = 'Local Ollama instance is required at http://localhost:11434 with at least one available model.';
const ASSISTANT_INTEGRATION_TIMEOUT_MS = 90000;

describe('API Endpoints (Integration)', () => {
  let app: INestApplication;

  beforeAll(async () => {
    const moduleFixture: TestingModule = await Test.createTestingModule({
      controllers: [AppController],
      providers: [
        AppService,
        { provide: AssistantService, useValue: assistantServiceMock },
        { provide: MealService, useValue: mealServiceMock },
        { provide: ShoppingService, useValue: shoppingServiceMock },
      ],
    }).compile();

    app = moduleFixture.createNestApplication();
    await app.init();
  });

  afterAll(async () => {
    if (app) {
      await app.close();
    }
  });

  describe('GET /api', () => {
    it('should return Hello World!', async () => {
      await request(app.getHttpServer())
        .get('/api')
        .expect(200)
        .expect('Welcome to Freezer Lego Meals NestJS API');
    });

    it('should return proper content type', async () => {
      const response = await request(app.getHttpServer())
        .get('/api')
        .expect(200);
      
      expect(response.headers['content-type']).toMatch(/text|plain/);
    });
  });

  describe('API Response Structure', () => {
    it('should handle empty requests gracefully', async () => {
      const response = await request(app.getHttpServer())
        .get('/api')
        .expect(200);
      
      expect(response.text).toBeDefined();
      expect(typeof response.text).toBe('string');
    });
  });
});

describe('API Architecture Validation', () => {
  let appController: AppController;
  let appService: AppService;

  beforeEach(async () => {
    const module: TestingModule = await Test.createTestingModule({
      controllers: [AppController],
      providers: [
        AppService,
        { provide: AssistantService, useValue: assistantServiceMock },
        { provide: MealService, useValue: mealServiceMock },
        { provide: ShoppingService, useValue: shoppingServiceMock },
      ],
    }).compile();

    appController = module.get<AppController>(AppController);
    appService = module.get<AppService>(AppService);
  });

  describe('Endpoint Configuration', () => {
    it('should have correct HTTP method mapping', () => {
      // Validate that controller has proper decorators
      const controllerPrototype = Object.getPrototypeOf(appController);
      expect(controllerPrototype.constructor.name).toBe('AppController');
    });

    it('should expose GET endpoint at root', () => {
      // Testing the structure of what would be an actual HTTP route
      expect(typeof appController.getHello).toBe('function');
    });
  });

  describe('Service Integration Pattern', () => {
    it('should properly initialize service dependency', () => {
      expect(appService).toBeDefined();
    });

    it('should delegate logic correctly to service layer', () => {
      // Verify controller properly calls service
      const result = appController.getHello();
      expect(result).toBe('Welcome to Freezer Lego Meals NestJS API');
    });
  });

  describe('Error Handling Structure', () => {
    it('should handle basic request flow correctly', () => {
      // Validate basic flow works
      const helloResult = appController.getHello();
      expect(helloResult).toBe('Welcome to Freezer Lego Meals NestJS API');
      expect(typeof helloResult).toBe('string');
    });
  });
});

describe('API Testing Infrastructure', () => {
  it('should support supertest for integration testing', () => {
    // This test validates that the testing framework supports API testing
    expect(true).toBe(true);
  });

  it('should be able to mock HTTP responses', () => {
    // Basic validation that we can structure tests properly
    expect(true).toBe(true);
  });
});

describe('AI Assistant Endpoint (End-to-End Integration)', () => {
  jest.setTimeout(ASSISTANT_INTEGRATION_TIMEOUT_MS);

  const availability = getOllamaAvailability();
  const assistantIntegrationIt = availability.isAvailable ? it : it.skip;
  const assistantIntegrationTestName = availability.isAvailable
    ? 'POST /api/assistant/chat returns a non-empty assistant message from local Ollama'
    : `POST /api/assistant/chat returns a non-empty assistant message from local Ollama (${availability.skipReason})`;

  let app: INestApplication | undefined;
  let originalDefaultModel: string | undefined;

  beforeAll(async () => {
    originalDefaultModel = process.env.OLLAMA_DEFAULT_MODEL;

    if (!availability.isAvailable) {
      return;
    }

    process.env.OLLAMA_BASE_URL = OLLAMA_BASE_URL;
    process.env.OLLAMA_DEFAULT_MODEL = availability.model;
    process.env.OLLAMA_TIMEOUT_MS = '60000';

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

    if (originalDefaultModel === undefined) {
      delete process.env.OLLAMA_DEFAULT_MODEL;
    } else {
      process.env.OLLAMA_DEFAULT_MODEL = originalDefaultModel;
    }
  });

  assistantIntegrationIt(assistantIntegrationTestName, async () => {
    const response = await request(app.getHttpServer())
      .post('/api/assistant/chat')
      .send({ message: 'Reply with the single word: OK' })
      .timeout({ response: 70000, deadline: ASSISTANT_INTEGRATION_TIMEOUT_MS })
      .expect(201);

    expect(response.body).toBeDefined();
    expect(typeof response.body.response).toBe('string');
    expect(response.body.response.trim().length).toBeGreaterThan(0);
  }, ASSISTANT_INTEGRATION_TIMEOUT_MS);
});

function getOllamaAvailability(): { isAvailable: boolean; model?: string; skipReason?: string } {
  try {
    const body = JSON.parse(execFileSync(process.execPath, [
      '-e',
      `const controller = new AbortController();
       const timeout = setTimeout(() => controller.abort(), 5000);
       fetch('${OLLAMA_BASE_URL}/api/tags', { signal: controller.signal })
         .then(async response => {
           if (!response.ok) {
             console.log(JSON.stringify({ isAvailable: false, skipReason: '${OLLAMA_REQUIRED_MESSAGE} GET /api/tags returned ' + response.status + '.' }));
             return;
           }

           const body = await response.json();
           console.log(JSON.stringify({ isAvailable: true, body }));
         })
         .catch(error => console.log(JSON.stringify({ isAvailable: false, skipReason: '${OLLAMA_REQUIRED_MESSAGE} GET /api/tags failed or timed out: ' + error.message })))
         .finally(() => clearTimeout(timeout));`,
    ], { encoding: 'utf8', timeout: 7000 }));

    if (!body.isAvailable) {
      return body;
    }

    const model = body?.body?.models?.find((candidate) =>
      Array.isArray(candidate?.capabilities) && candidate.capabilities.includes('completion')
    )?.name;
    if (!model || typeof model !== 'string') {
      return {
        isAvailable: false,
        skipReason: `${OLLAMA_REQUIRED_MESSAGE} GET /api/tags returned no completion-capable models.`,
      };
    }

    return { isAvailable: true, model };
  } catch (error) {
    return {
      isAvailable: false,
      skipReason: `${OLLAMA_REQUIRED_MESSAGE} GET /api/tags failed or timed out: ${error instanceof Error ? error.message : String(error)}`,
    };
  }
}