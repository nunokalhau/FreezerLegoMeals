import { Test, TestingModule } from '@nestjs/testing';
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

describe('AppController', () => {
  let appController: AppController;
  let appService: AppService;

  beforeEach(async () => {
    const app: TestingModule = await Test.createTestingModule({
      controllers: [AppController],
      providers: [
        AppService,
        { provide: MealService, useValue: mealServiceMock },
        { provide: ShoppingService, useValue: shoppingServiceMock },
      ],
    }).compile();

    appController = app.get<AppController>(AppController);
    appService = app.get<AppService>(AppService);
  });

  describe('root', () => {
    it('should return "Hello World!"', () => {
      expect(appController.getHello()).toBe('Welcome to Freezer Lego Meals NestJS API');
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
      expect(appService.getHello()).toBe('Welcome to Freezer Lego Meals NestJS API');
    });
  });
});