import { Test, TestingModule } from '@nestjs/testing';
import { AppController } from './app.controller';
import { AppService } from './app.service';

describe('AppController', () => {
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

  describe('root', () => {
    it('should return "Hello World!"', () => {
      expect(appController.getHello()).toBe('Hello World!');
    });
  });

  describe('controller structure', () => {
    it('should be defined', () => {
      expect(appController).toBeDefined();
    });

    it('should have getHello method', () => {
      expect(typeof appController.getHello).toBe('function');
    });
  });

  describe('app service integration', () => {
    it('should properly inject service', () => {
      expect(appService).toBeDefined();
    });

    it('should provide correct hello message', () => {
      expect(appService.getHello()).toBe('Hello World!');
    });
  });
});