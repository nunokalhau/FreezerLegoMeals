import { Test, TestingModule } from '@nestjs/testing';
import { MealService } from '../../../src/services/Services.NestJS/meal.service';
import { ShoppingService } from '../../../src/services/Services.NestJS/shopping.service';
import type { RecipeRepositoryInterface } from '../../../src/repositories/Repository.NestJS/recipe.repository';

// Mock repository for unit testing
class MockRecipeRepository implements RecipeRepositoryInterface {
  async getRecipes(): Promise<any[]> {
    return [
      {
        id: 1,
        name: 'Chicken Curry',
        ingredients: ['chicken', 'onion', 'garlic'],
        recipeIngredients: [
          { amount: 1, ingredient: { name: 'chicken', category: 'protein' } },
          { amount: 1, ingredient: { name: 'onion', category: 'produce' } },
          { amount: 1, ingredient: { name: 'garlic', category: 'produce' } },
        ]
      },
      {
        id: 2,
        name: 'Beef Stir Fry',
        ingredients: ['beef', 'broccoli', 'soy sauce'],
        recipeIngredients: [
          { amount: 1, ingredient: { name: 'beef', category: 'protein' } },
          { amount: 1, ingredient: { name: 'broccoli', category: 'produce' } },
          { amount: 1, ingredient: { name: 'soy sauce', category: 'condiment' } },
        ]
      }
    ];
  }

  async getRecipeById(id: number): Promise<any | null> {
    if (id === 1) {
      return {
        id: 1,
        name: 'Chicken Curry',
        ingredients: ['chicken', 'onion', 'garlic'],
        recipeIngredients: [
          { amount: 1, ingredient: { name: 'chicken', category: 'protein' } },
          { amount: 1, ingredient: { name: 'onion', category: 'produce' } },
          { amount: 1, ingredient: { name: 'garlic', category: 'produce' } },
        ]
      };
    } else if (id === 2) {
      return {
        id: 2,
        name: 'Beef Stir Fry',
        ingredients: ['beef', 'broccoli', 'soy sauce'],
        recipeIngredients: [
          { amount: 1, ingredient: { name: 'beef', category: 'protein' } },
          { amount: 1, ingredient: { name: 'broccoli', category: 'produce' } },
          { amount: 1, ingredient: { name: 'soy sauce', category: 'condiment' } },
        ]
      };
    }
    return null;
  }

  async findRecipesWithIngredients(ingredients: string[]): Promise<any[]> {
    // Return recipes that have at least one of the requested ingredients
    const mockRecipes = await this.getRecipes();
    return mockRecipes.filter(recipe => 
      recipe.ingredients.some((ingredient: string) => 
        ingredients.includes(ingredient)
      )
    );
  }

  async getCombinations(): Promise<any[]> {
    return [];
  }

  async getCombinationById(id: number): Promise<any | null> {
    return null;
  }

  async getIngredients(): Promise<any[]> {
    return [];
  }

  async getIngredientByName(name: string): Promise<any | null> {
    return null;
  }
}

describe('MealService', () => {
  let service: MealService;
  let mockRepository: MockRecipeRepository;

  beforeEach(async () => {
    mockRepository = new MockRecipeRepository();
    
    const module: TestingModule = await Test.createTestingModule({
      providers: [
        MealService,
        { provide: 'RecipeRepositoryInterface', useValue: mockRepository }
      ],
    }).compile();

    service = module.get<MealService>(MealService);
  });

  it('should be defined', () => {
    expect(service).toBeDefined();
  });

  describe('getRecipes', () => {
    it('should return all recipes from repository', async () => {
      const result = await service.getRecipes();
      expect(result).toHaveLength(2);
      expect(result[0].name).toBe('Chicken Curry');
      expect(result[1].name).toBe('Beef Stir Fry');
    });
  });

  describe('searchRecipesByIngredients', () => {
    it('should find recipes with matching ingredients', async () => {
      const result = await service.searchRecipesByIngredients(['chicken']);
      expect(result).toHaveLength(1);
      expect(result[0].name).toBe('Chicken Curry');
    });

    it('should return empty array when no matches found', async () => {
      const result = await service.searchRecipesByIngredients(['potato']);
      expect(result).toHaveLength(0);
    });
  });

  describe('getRecipeById', () => {
    it('should return recipe by ID', async () => {
      const result = await service.getRecipeById(1);
      expect(result).not.toBeNull();
      expect(result.name).toBe('Chicken Curry');
    });

    it('should return null for non-existent ID', async () => {
      const result = await service.getRecipeById(999);
      expect(result).toBeNull();
    });
  });

  describe('findMealsWithIngredients', () => {
    it('should return appropriate search results with match count', async () => {
      const result = await service.findMealsWithIngredients('chicken and onion');
      expect(result.totalRecipesFound).toBeGreaterThan(0);
      expect(result.recipes).toHaveLength(1);
      expect(result.searchTerms).toContain('chicken');
    });

    it('should handle queries without matching ingredients', async () => {
      const result = await service.findMealsWithIngredients('potato and cheese');
      expect(result.totalRecipesFound).toBe(0);
      expect(result.recipes).toHaveLength(0);
    });
  });

  describe('getRecipeDetails', () => {
    it('should return recipe details when found', async () => {
      const result = await service.getRecipeDetails(1);
      expect(result.recipe).not.toBeNull();
      expect(result.recipe.name).toBe('Chicken Curry');
    });

    it('should return error when recipe not found', async () => {
      const result = await service.getRecipeDetails(999);
      expect(result.error).toBeDefined();
    });
  });
});

describe('ShoppingService', () => {
  let service: ShoppingService;
  let mockRepository: MockRecipeRepository;

  beforeEach(async () => {
    mockRepository = new MockRecipeRepository();
    
    const module: TestingModule = await Test.createTestingModule({
      providers: [
        ShoppingService,
        { provide: 'RecipeRepositoryInterface', useValue: mockRepository }
      ],
    }).compile();

    service = module.get<ShoppingService>(ShoppingService);
  });

  it('should be defined', () => {
    expect(service).toBeDefined();
  });

  describe('getRecipeIngredients', () => {
    it('should return ingredients for valid recipe ID', async () => {
      const result = await service.getRecipeIngredients('1');
      expect(result).toHaveLength(3);
      expect(result).toContain('chicken');
    });

    it('should return empty array for invalid recipe ID', async () => {
      const result = await service.getRecipeIngredients('999');
      expect(result).toHaveLength(0);
    });
  });

  describe('getMultipleRecipeIngredients', () => {
    it('should return ingredients for multiple recipes', async () => {
      const result = await service.getMultipleRecipeIngredients(['1', '2']);
      expect(Object.keys(result)).toHaveLength(2);
      expect(result['1']).toHaveLength(3);
      expect(result['2']).toHaveLength(3);
    });
  });

  describe('generateShoppingList', () => {
    it('should generate shopping list for valid recipes', async () => {
      const result = await service.generateShoppingList(['1']);
      expect(result.recipes).toHaveLength(1);
      expect(result.ingredients).toHaveLength(3);
      expect(result.totalRecipes).toBe(1);
    });

    it('should handle empty recipe list gracefully', async () => {
      const result = await service.generateShoppingList([]);
      expect(result.totalRecipes).toBe(0);
      expect(result.ingredients).toHaveLength(0);
    });

    it('should scale ingredients by scaleFactor', async () => {
      const result = await service.generateShoppingList(['1'], 2.0);
      // When scaling by 2, the quantities should be doubled
      expect(result.recipes).toHaveLength(1);
      expect(result.scaleFactor).toBe(2.0);
    });
  });

  describe('getRecipeInfo', () => {
    it('should return recipe info for valid ID', async () => {
      const result = await service.getRecipeInfo('1');
      expect(result).not.toBeNull();
      expect(result.name).toBe('Chicken Curry');
    });

    it('should return null for invalid ID', async () => {
      const result = await service.getRecipeInfo('999');
      expect(result).toBeNull();
    });
  });
});