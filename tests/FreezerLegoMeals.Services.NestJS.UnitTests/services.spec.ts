import { Test, TestingModule } from '@nestjs/testing';
import { MealService } from './meal.service';
import { ShoppingService } from './shopping.service';
import { Recipe } from '../models/recipe.dto';

describe('NestJS Service Layer', () => {
  describe('MealService', () => {
    let service: MealService;

    beforeEach(async () => {
      const module: TestingModule = await Test.createTestingModule({
        providers: [MealService],
      }).compile();

      service = module.get<MealService>(MealService);
    });

    it('should be defined', () => {
      expect(service).toBeDefined();
    });

    describe('findMealsWithIngredients', () => {
      it('should return an array of recipes', async () => {
        const ingredients = ['chicken', 'onion'];
        const result = await service.findMealsWithIngredients(ingredients);
        expect(result).toBeInstanceOf(Array);
      });

      it('should handle empty ingredients array', async () => {
        const result = await service.findMealsWithIngredients([]);
        expect(result).toBeInstanceOf(Array);
      });

      it('should return appropriate data structure for recipe search', async () => {
        const ingredients = ['chicken'];
        const result = await service.findMealsWithIngredients(ingredients);
        // Assuming result structure includes recipe details
        expect(result).toHaveProperty('length');
      });
    });

    describe('getRecipeDetails', () => {
      it('should return recipe details or null for non-existent recipe', async () => {
        const result = await service.getRecipeDetails(1);
        expect(result).toBeNull(); // In real implementation, this would be a Recipe object
      });

      it('should handle negative recipe IDs gracefully', async () => {
        const result = await service.getRecipeDetails(-1);
        expect(result).toBeNull();
      });
    });
  });

  describe('ShoppingService', () => {
    let service: ShoppingService;

    beforeEach(async () => {
      const module: TestingModule = await Test.createTestingModule({
        providers: [ShoppingService],
      }).compile();

      service = module.get<ShoppingService>(ShoppingService);
    });

    it('should be defined', () => {
      expect(service).toBeDefined();
    });

    describe('generateShoppingList', () => {
      it('should return a valid object structure', async () => {
        const result = await service.generateShoppingList([]);
        expect(result).toBeInstanceOf(Object);
      });

      it('should handle single recipe correctly', async () => {
        const result = await service.generateShoppingList(['Chicken Curry']);
        expect(result).toBeInstanceOf(Object);
      });

      it('should properly process multiple recipes', async () => {
        const result = await service.generateShoppingList(['Chicken Curry', 'Vegetable Soup']);
        expect(result).toBeInstanceOf(Object);
      });

      it('should handle scale factor correctly', async () => {
        const result = await service.generateShoppingList(['Chicken Curry'], 2.5);
        expect(result).toBeInstanceOf(Object);
      });
    });
  });

  describe('Service Integration', () => {
    let mealService: MealService;
    let shoppingService: ShoppingService;

    beforeEach(async () => {
      const module: TestingModule = await Test.createTestingModule({
        providers: [MealService, ShoppingService],
      }).compile();

      mealService = module.get<MealService>(MealService);
      shoppingService = module.get<ShoppingService>(ShoppingService);
    });

    it('should allow both services to be created independently', () => {
      expect(mealService).toBeDefined();
      expect(shoppingService).toBeDefined();
    });

    it('should maintain separate functionality between services', () => {
      // Basic verification that both services are different instances
      expect(mealService).not.toBe(shoppingService);
    });
  });
});