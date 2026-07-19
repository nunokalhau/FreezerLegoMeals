import { MealService } from '../../../services/Services.NestJS/meal.service';
import { ShoppingService } from '../../../services/Services.NestJS/shopping.service';
import type { RecipeRepositoryInterface } from '../../../repositories/Repository.NestJS/recipe.repository';

describe('NestJS service functional behavior', () => {
  let repo: jest.Mocked<RecipeRepositoryInterface>;

  beforeEach(() => {
    repo = {
      getRecipes: jest.fn(),
      getRecipeById: jest.fn(),
      findRecipesWithIngredients: jest.fn(),
      getCombinations: jest.fn(),
      getCombinationById: jest.fn(),
      getIngredients: jest.fn(),
      getIngredientByName: jest.fn(),
    };
  });

  it('MealService.findMealsWithIngredients extracts food terms', async () => {
    repo.findRecipesWithIngredients.mockResolvedValue([] as any);
    const service = new MealService(repo);

    await service.findMealsWithIngredients('Need CHICKEN and tomato');

    expect(repo.findRecipesWithIngredients).toHaveBeenCalledWith(expect.arrayContaining(['chicken', 'tomato']));
  });

  it('ShoppingService.generateShoppingList returns validation error when scale <= 0', async () => {
    const service = new ShoppingService(repo);

    const result = await service.generateShoppingList(['1'], 0, true);

    expect(result.ingredients).toHaveLength(0);
    expect(result.message).toContain('Scale factor must be greater than 0');
  });

  it('ShoppingService.getRecipeIngredients resolves by name through repository search', async () => {
    repo.findRecipesWithIngredients.mockResolvedValue([
      {
        id: 1,
        recipeIngredients: [{ amount: 2, unit: 'g', ingredient: { name: 'chicken', category: 'protein' } }],
      } as any,
    ]);

    const service = new ShoppingService(repo);

    const ingredients = await service.getRecipeIngredients('Chicken');

    expect(repo.findRecipesWithIngredients).toHaveBeenCalledWith(['Chicken']);
    expect(ingredients).toHaveLength(1);
    expect(ingredients[0].ingredient.name).toBe('chicken');
  });

  it('ShoppingService.getRecipeInfo returns null for blank identifier', async () => {
    const service = new ShoppingService(repo);

    const result = await service.getRecipeInfo('   ');

    expect(result).toBeNull();
  });

  it('ShoppingService.generateShoppingList aggregates duplicate ingredients across recipes', async () => {
    const service = new ShoppingService(repo);
    const spy = jest.spyOn(service, 'getMultipleRecipeIngredients').mockResolvedValue({
      r1: [{ amount: 1, unit: 'g', ingredient: { name: 'salt', category: 'condiments' } } as any],
      r2: [{ amount: 3, unit: 'g', ingredient: { name: 'salt', category: 'condiments' } } as any],
    });

    const result = await service.generateShoppingList(['r1', 'r2'], 1, true);

    expect(spy).toHaveBeenCalled();
    expect(result.ingredients).toHaveLength(1);
    expect(result.ingredients[0].quantity).toBe(4);
  });
});
