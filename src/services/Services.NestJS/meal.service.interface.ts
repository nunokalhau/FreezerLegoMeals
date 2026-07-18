export interface IMealService {
  searchRecipesByIngredients(ingredients: string[]): Promise<any[]>;
  getRecipeById(recipeId: number): Promise<any | null>;
  findMealsWithIngredients(query: string): Promise<any>;
  getRecipeDetails(recipeId: number): Promise<any>;
}