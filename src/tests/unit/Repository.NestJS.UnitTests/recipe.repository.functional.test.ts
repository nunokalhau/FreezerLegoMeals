import { RecipeRepository } from '../../../repositories/Repository.NestJS/recipe.repository';
import * as fs from 'fs';

jest.mock('fs', () => ({
  existsSync: jest.fn(),
}));

let mockAll: jest.Mock;
let mockGet: jest.Mock;
let mockClose: jest.Mock;
let mockDatabaseCtor: jest.Mock;

jest.mock('sqlite3', () => ({
  __esModule: true,
  default: {
    OPEN_READONLY: 1,
    Database: function (...args: unknown[]) {
      return mockDatabaseCtor(...args);
    },
  },
}));

describe('RecipeRepository functional behavior', () => {
  beforeEach(() => {
    mockAll = jest.fn();
    mockGet = jest.fn();
    mockClose = jest.fn();
    mockDatabaseCtor = jest.fn().mockImplementation((_dbPath, _mode, onOpen) => {
      if (onOpen) {
        onOpen(null);
      }

      return {
        all: mockAll,
        get: mockGet,
        close: mockClose,
      };
    });

    jest.clearAllMocks();
  });

  it('getRecipes maps rows and recipe ingredients from sqlite', async () => {
    (fs.existsSync as unknown as jest.Mock).mockReturnValue(true);

    mockAll.mockImplementation((query: string, params: unknown[], cb: (err: Error | null, rows: any[]) => void) => {
      if (query.includes('FROM recipes') && !query.includes('recipe_ingredients')) {
        cb(null, [
          {
            id: 1,
            name: 'Chicken Soup',
            source_path: 'proteins/chicken_soup.md',
            tags: 'soup',
            servings: 2,
            time_to_prepare: 30,
            prepping: 'prep',
            freezing_notes: 'freeze',
            reheat_notes: 'reheat',
            combinations: '',
            notes: 'note',
          },
        ]);
        return;
      }

      if (query.includes('FROM recipe_ingredients')) {
        cb(null, [
          {
            id: 10,
            recipe_id: 1,
            ingredient_id: 5,
            amount: 2,
            unit: 'g',
            ingredient_name: 'chicken',
          },
        ]);
        return;
      }

      cb(null, []);
    });

    const repository = new RecipeRepository();

    const recipes = await repository.getRecipes();

    expect(recipes).toHaveLength(1);
    expect(recipes[0].name).toBe('Chicken Soup');
    expect(recipes[0].recipeIngredients).toHaveLength(1);
    expect(recipes[0].recipeIngredients?.[0].ingredient?.name).toBe('chicken');
  });

  it('findRecipesWithIngredients returns empty for blank inputs', async () => {
    (fs.existsSync as unknown as jest.Mock).mockReturnValue(true);
    const repository = new RecipeRepository();

    const results = await repository.findRecipesWithIngredients(['   ', '']);

    expect(results).toEqual([]);
    expect(mockAll).not.toHaveBeenCalled();
  });

  it('getIngredientByName trims input before query', async () => {
    (fs.existsSync as unknown as jest.Mock).mockReturnValue(true);

    mockGet.mockImplementation((_query: string, params: unknown[], cb: (err: Error | null, row: any) => void) => {
      expect(params[0]).toBe('Garlic');
      cb(null, { id: 8, name: 'Garlic' });
    });

    const repository = new RecipeRepository();

    const ingredient = await repository.getIngredientByName('  Garlic  ');

    expect(ingredient).toEqual({ id: 8, name: 'Garlic' });
  });
});
