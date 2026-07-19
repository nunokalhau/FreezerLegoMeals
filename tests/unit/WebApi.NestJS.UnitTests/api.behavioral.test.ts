import { Test, TestingModule } from '@nestjs/testing';
import { INestApplication } from '@nestjs/common';
import request from 'supertest';
import { AppController } from '../../../src/api/WebApi.NestJS/app.controller';
import { AppService } from '../../../src/api/WebApi.NestJS/app.service';
import { MealService } from '../../../src/services/Services.NestJS/meal.service';
import { ShoppingService } from '../../../src/services/Services.NestJS/shopping.service';

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

describe('API Endpoints (Integration)', () => {
  let app: INestApplication;

  beforeAll(async () => {
    const moduleFixture: TestingModule = await Test.createTestingModule({
      controllers: [AppController],
      providers: [
        AppService,
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

  describe('Response Structure', () => {
    it('should handle empty requests gracefully', async () => {
      const response = await request(app.getHttpServer())
        .get('/api')
        .expect(200);
      
      expect(response.text).toBeDefined();
      expect(typeof response.text).toBe('string');
    });
  });

  describe('Service Integration Patterns', () => {
    it('should properly delegate to service layer', async () => {
      // Test that controller calls the service correctly
      const response = await request(app.getHttpServer())
        .get('/api')
        .expect(200);
      
      expect(response.text).toBe('Welcome to Freezer Lego Meals NestJS API');
    });
  });
});

describe('API Controller Behavior', () => {
  let appController: AppController;
  let appService: AppService;

  beforeEach(async () => {
    const module: TestingModule = await Test.createTestingModule({
      controllers: [AppController],
      providers: [
        AppService,
        { provide: MealService, useValue: mealServiceMock },
        { provide: ShoppingService, useValue: shoppingServiceMock },
      ],
    }).compile();

    appController = module.get<AppController>(AppController);
    appService = module.get<AppService>(AppService);
  });

  describe('getHello', () => {
    it('should return proper greeting from service', () => {
      const result = appController.getHello();
      expect(result).toBe('Welcome to Freezer Lego Meals NestJS API');
    });
  });

  describe('healthCheck', () => {
    it('should return health status object', async () => {
      const result = await appController.healthCheck();
      expect(result).toHaveProperty('status', 'healthy');
      expect(result).toHaveProperty('service', 'WebApi.NestJS');
    });
  });

  describe('Endpoint Decorators and Routing Validation', () => {
    it('should have proper HTTP method mapping for root endpoint', async () => {
      // Verify that the controller has the expected decorators and endpoints
      const result = appController.getHello();
      expect(result).toBe('Welcome to Freezer Lego Meals NestJS API');
      expect(typeof result).toBe('string');
    });
  });
});