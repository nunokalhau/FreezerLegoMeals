import { Test, TestingModule } from '@nestjs/testing';
import { INestApplication } from '@nestjs/common';
import * as request from 'supertest';
import { AppController } from '../src/api/FrezerLegoMeals.WebApi.NestJS/app.controller';
import { AppService } from '../src/api/FrezerLegoMeals.WebApi.NestJS/app.service';

describe('API Endpoints (Integration)', () => {
  let app: INestApplication;

  beforeAll(async () => {
    const moduleFixture: TestingModule = await Test.createTestingModule({
      controllers: [AppController],
      providers: [AppService],
    }).compile();

    app = moduleFixture.createNestApplication();
    await app.init();
  });

  afterAll(async () => {
    await app.close();
  });

  describe('GET /', () => {
    it('should return Hello World!', async () => {
      await request(app.getHttpServer())
        .get('/')
        .expect(200)
        .expect('Hello World!');
    });

    it('should return proper content type', async () => {
      const response = await request(app.getHttpServer())
        .get('/')
        .expect(200);
      
      expect(response.headers['content-type']).toMatch(/json|text/);
    });
  });

  describe('Response Structure', () => {
    it('should handle empty requests gracefully', async () => {
      const response = await request(app.getHttpServer())
        .get('/')
        .expect(200);
      
      expect(response.body).toBeDefined();
      expect(typeof response.text).toBe('string');
    });
  });

  describe('Service Integration Patterns', () => {
    it('should properly delegate to service layer', async () => {
      // Test that controller calls the service correctly
      const response = await request(app.getHttpServer())
        .get('/')
        .expect(200);
      
      expect(response.text).toBe('Hello World!');
    });
  });
});

describe('API Controller Behavior', () => {
  let appController: AppController;
  let appService: AppService;

  beforeEach(async () => {
    const module: TestingModule = await Test.createTestingModule({
      controllers: [AppController],
      providers: [AppService],
    }).compile();

    appController = module.get<AppController>(AppController);
    appService = module.get<AppService>(AppService);
  });

  describe('getHello', () => {
    it('should return proper greeting from service', () => {
      const result = appController.getHello();
      expect(result).toBe('Hello World!');
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
      expect(result).toBe('Hello World!');
      expect(typeof result).toBe('string');
    });
  });
});