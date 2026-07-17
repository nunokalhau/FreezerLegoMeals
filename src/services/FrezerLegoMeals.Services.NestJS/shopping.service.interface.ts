import { Recipe } from '../models/recipe.dto';

export interface ShoppingServiceInterface {
  generateShoppingList(recipes: string[], scaleFactor?: number): Promise<any>;
}