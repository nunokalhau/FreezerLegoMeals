import { Injectable } from '@nestjs/common';
import { IShoppingService } from './shopping.service.interface';
import { RecipeRepositoryInterface } from '../../repositories/Repository.NestJS/recipe.repository';
import { ShoppingListResponse } from './models/shopping-list-response.dto';
import { RecipeInfoResponse } from './models/recipe-info-response.dto';
import { ShoppingListItem } from './models/shopping-list-item.dto';
import { Recipe } from './models/recipe.dto';

@Injectable()
export class ShoppingService implements IShoppingService {
  constructor(
    private readonly recipeRepository: RecipeRepositoryInterface
  ) {}

  async getRecipeIngredients(recipeIdentifier: string): Promise<any[]> {
    const parsedId = parseInt(recipeIdentifier, 10);
    let recipe;
    
    if (!isNaN(parsedId)) {
      recipe = await this.recipeRepository.getRecipeById(parsedId);
    } else {
      return [];
    }

    if (!recipe) {
      return [];
    }
    
    return recipe.ingredients || [];
  }

  async getMultipleRecipeIngredients(recipeIdentifiers: string[]): Promise<Record<string, any[]>> {
    const result: Record<string, any[]> = {};
    
    for (const identifier of recipeIdentifiers) {
      result[identifier] = await this.getRecipeIngredients(identifier);
    }
    
    return result;
  }

  async generateShoppingList(
    recipeIdentifiers: string[], 
    scaleFactor: number = 1.0, 
    groupByCategory: boolean = true
  ): Promise<ShoppingListResponse> {
    if (!recipeIdentifiers || recipeIdentifiers.length === 0) {
      return new ShoppingListResponse({
        recipes: recipeIdentifiers,
        totalRecipes: 0,
        scaleFactor,
        ingredients: [],
        message: "No recipes provided to generate shopping list"
      });
    }

    try {
      // Get all recipes to extract ingredients
      const allRecipes: Recipe[] = [];
      for (const identifier of recipeIdentifiers) {
        const parsedId = parseInt(identifier, 10);
        if (!isNaN(parsedId)) {
          const recipe = await this.recipeRepository.getRecipeById(parsedId);
          if (recipe) {
            allRecipes.push(recipe);
          }
        }
      }

      // Aggregate ingredients from all recipes
      const ingredientMap: Record<string, { name: string; quantity: number; unit: string }> = {};
      
      for (const recipe of allRecipes) {
        if (recipe.ingredients && Array.isArray(recipe.ingredients)) {
          for (const ingredient of recipe.ingredients) {
            // Handle both string ingredients and structured ingredients
            let ingredientName: string;
            let ingredientQuantity: number = 1;
            let ingredientUnit: string = '';
            
            if (typeof ingredient === 'string') {
              ingredientName = ingredient;
            } else {
              ingredientName = ingredient.name || '';
              ingredientQuantity = ingredient.quantity || 1;
              ingredientUnit = ingredient.unit || '';
            }
            
            // Scale the quantities
            const scaledQuantity = ingredientQuantity * scaleFactor;
            
            if (ingredientMap[ingredientName]) {
              // If we've seen this ingredient, add to existing quantity  
              ingredientMap[ingredientName].quantity += scaledQuantity;
            } else {
              ingredientMap[ingredientName] = {
                name: ingredientName,
                quantity: scaledQuantity,
                unit: ingredientUnit
              };
            }
          }
        }
      }

      // Convert ingredients map back to list format for response
      const ingredients: ShoppingListItem[] = Object.values(ingredientMap).map(item => ({
        name: item.name,
        quantity: item.quantity,
        unit: item.unit
      }));

      return new ShoppingListResponse({
        recipes: recipeIdentifiers,
        totalRecipes: recipeIdentifiers.length,
        scaleFactor,
        ingredients,
        message: `Generated shopping list for ${recipeIdentifiers.length} recipes scaled by factor ${scaleFactor}`
      });
    } catch (error) {
      // Return error response in case of failure
      return new ShoppingListResponse({
        recipes: recipeIdentifiers,
        totalRecipes: recipeIdentifiers.length,
        scaleFactor,
        ingredients: [],
        message: `Error generating shopping list: ${(error as Error).message || 'Unknown error'}`
      });
    }
  }

  async getRecipeInfo(recipeIdentifier: string): Promise<RecipeInfoResponse | null> {
    const parsedId = parseInt(recipeIdentifier, 10);
    let recipe;
    
    if (!isNaN(parsedId)) {
      recipe = await this.recipeRepository.getRecipeById(parsedId);
    } else {
      recipe = null; 
    }
    
    if (recipe) {
      return new RecipeInfoResponse({
        id: recipe.id,
        name: recipe.name,
        servings: recipe.servings,
        timeToPrepare: recipe.timeToPrepare
      });
    }
    
    return null;
  }
}