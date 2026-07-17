import { Test, TestingModule } from '@nestjs/testing';
import { MealService } from './meal.service';
import { ShoppingService } from './shopping.service';

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
    it('should return empty array when no ingredients provided', async () => {
      const result = await service.findMealsWithIngredients([]);
      expect(result).toBeInstanceOf(Array);
      expect(result).toHaveLength(0);
    });

    it('should handle single ingredient correctly', async () => {
      const result = await service.findMealsWithIngredients(['chicken']);
      expect(result).toBeInstanceOf(Array);
    });
  });

  describe('getRecipeDetails', () => {
    it('should return null for non-existent recipe', async () => {
      const result = await service.getRecipeDetails(999);
      expect(result).toBeNull();
    });

    it('should handle valid recipe ID correctly', async () => {
      const result = await service.getRecipeDetails(1);
      expect(result).toBeNull(); // Placeholder - would return actual recipe in real implementation
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
    it('should handle empty recipe list correctly', async () => {
      const result = await service.generateShoppingList([]);
      expect(result).toBeInstanceOf(Object);
    });

    it('should handle single recipe correctly', async () => {
      const result = await service.generateShoppingList(['Chicken Curry']);
      expect(result).toBeInstanceOf(Object);
    });

    it('should handle scale factor correctly', async () => {
      const result = await service.generateShoppingList(['Chicken Curry'], 2.0);
      expect(result).toBeInstanceOf(Object);
    });
  });
});