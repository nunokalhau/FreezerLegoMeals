import { IngredientSearchResponse } from './models/ingredient-search-response.dto';
import { Recipe } from './models/recipe.dto';
import { RecipeDetailsResponse } from './models/recipe-details-response.dto';

export interface IMealService {
  getRecipes(): Promise<Recipe[]>;
  searchRecipesByIngredients(ingredients: string[]): Promise<Recipe[]>;
  getRecipeById(recipeId: number): Promise<Recipe | null>;
  findMealsWithIngredients(query: string): Promise<IngredientSearchResponse>;
  getRecipeDetails(recipeId: number): Promise<RecipeDetailsResponse>;
}