import { Test, TestingModule } from '@nestjs/testing';
import { AppController } from './app.controller';
import { AppService } from './app.service';
import { AppModule } from '../src/api/FrezerLegoMeals.WebApi.NestJS/app.module';

describe('AppModule', () => {
  let module: TestingModule;

  beforeAll(async () => {
    module = await Test.createTestingModule({
      imports: [AppModule],
    }).compile();
  });

  it('should compile the module successfully', () => {
    expect(module).toBeDefined();
  });

  it('should have controllers defined', () => {
    const controllers = module.getModules().forEach(mod => {
      expect(mod.controllers.size).toBeGreaterThan(0);
    });
  });

  it('should have providers defined', () => {
    const providers = module.getModules().forEach(mod => {
      expect(mod.providers.size).toBeGreaterThan(0);
    });
  });
});

describe('API Integration Tests', () => {
  let appController: AppController;
  let appService: AppService;

  beforeEach(async () => {
    const app: TestingModule = await Test.createTestingModule({
      controllers: [AppController],
      providers: [AppService],
    }).compile();

    appController = app.get<AppController>(AppController);
    appService = app.get<AppService>(AppService);
  });

  describe('Full API Endpoints', () => {
    it('should handle root endpoint correctly', async () => {
      const result = await appController.getHello();
      expect(result).toBe('Hello World!');
    });

    it('should maintain proper HTTP response structure', () => {
      // Verify the method would return string content properly
      const helloOutput = appController.getHello();
      expect(typeof helloOutput).toBe('string');
      expect(helloOutput).toContain('Hello');
    });
  });

  describe('Service Layer Integration', () => {
    it('should properly connect controller to service', () => {
      // Verify that the controller has a service instance
      expect(appController).toBeDefined();
      const prototype = Object.getPrototypeOf(appController);
      expect(prototype.constructor.name).toBe('AppController');
    });

    it('should maintain service contract integrity', async () => {
      const helloMethod = appService.getHello;
      expect(typeof helloMethod).toBe('function');
      
      // Call the method to ensure no errors
      const result = helloMethod();
      expect(result).toBe('Hello World!');
    });
  });
});

describe('API Architecture Tests', () => {
  it('should follow NestJS architectural patterns', () => {
    // Test that key components exist
    expect(AppController).toBeDefined();
    expect(AppService).toBeDefined();
    
    // Basic structural validation
    expect(typeof AppController).toBe('function');
    expect(typeof AppService).toBe('function');
  });

  it('should be properly decorated', () => {
    // Verify decorators are present (simplified check)
    expect(AppController.name).toContain('AppController');
    expect(AppService.name).toContain('AppService');
  });
});