import { RecipeInfoResponse } from './models/recipe-info-response.dto';
import { ShoppingListResponse } from './models/shopping-list-response.dto';
import { RecipeIngredient } from './models/recipe.dto';

export interface IShoppingService {
  getRecipeIngredients(recipeIdentifier: string): Promise<RecipeIngredient[]>;
  getMultipleRecipeIngredients(recipeIdentifiers: string[]): Promise<Record<string, RecipeIngredient[]>>;
  generateShoppingList(
    recipeIdentifiers: string[],
    scaleFactor?: number,
    groupByCategory?: boolean
  ): Promise<ShoppingListResponse>;
  getRecipeInfo(recipeIdentifier: string): Promise<RecipeInfoResponse | null>;
}