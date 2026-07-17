import { Recipe } from '../models/recipe.dto';

export interface MealServiceInterface {
  findMealsWithIngredients(ingredients: string[]): Promise<Recipe[]>;
  getRecipeDetails(recipeId: number): Promise<Recipe | null>;
}