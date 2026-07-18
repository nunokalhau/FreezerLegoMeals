export interface IShoppingService {
  getRecipeIngredients(recipeIdentifier: string): Promise<any[]>;
  getMultipleRecipeIngredients(recipeIdentifiers: string[]): Promise<Record<string, any[]>>;
  generateShoppingList(
    recipeIdentifiers: string[], 
    scaleFactor?: number, 
    groupByCategory?: boolean
  ): Promise<any>;
  getRecipeInfo(recipeIdentifier: string): Promise<any | null>;
}