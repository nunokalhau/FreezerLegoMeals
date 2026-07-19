import { Inject, Injectable } from '@nestjs/common';
import { IShoppingService } from './shopping.service.interface';
import { RecipeRepositoryInterface } from '../../repositories/Repository.NestJS/recipe.repository';
import { ShoppingListResponse } from './models/shopping-list-response.dto';
import { RecipeInfoResponse } from './models/recipe-info-response.dto';
import { ShoppingListItem } from './models/shopping-list-item.dto';
import { Recipe } from './models/recipe.dto';
import { plainToInstance } from 'class-transformer';

@Injectable()
export class ShoppingService implements IShoppingService {
  constructor(
    @Inject('RecipeRepositoryInterface')
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
      return plainToInstance(ShoppingListResponse, {
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

      const ingredientMap: Record<string, ShoppingListItem> = {};
      
      for (const recipe of allRecipes) {
        if (recipe && recipe.recipeIngredients && Array.isArray(recipe.recipeIngredients)) {
          for (const recipeIngredient of recipe.recipeIngredients) {
            const ingredientKey = `${recipeIngredient.ingredient.name}-${recipeIngredient.ingredient.category}`;
            
            if (!ingredientMap[ingredientKey]) {
              ingredientMap[ingredientKey] = {
                name: recipeIngredient.ingredient.name,
                quantity: 0,
                unit: 'units', // Defaulting to 'units' instead of trying to access .unit
                category: recipeIngredient.ingredient.category
              };
            }
            
            // Add quantity from this recipeIngredient
            const quantityToAdd = recipeIngredient.amount || 1;
            ingredientMap[ingredientKey].quantity += quantityToAdd * scaleFactor;
          }
        }
      }

      // Convert ingredients map back to list format for response  
      const ingredients: ShoppingListItem[] = Object.values(ingredientMap).map(item => ({
        name: item.name,
        quantity: item.quantity,
        unit: item.unit,
        category: item.category
      }));

      return plainToInstance(ShoppingListResponse, {
        recipes: recipeIdentifiers,
        totalRecipes: recipeIdentifiers.length,
        scaleFactor,
        ingredients,
        message: `Generated shopping list for ${recipeIdentifiers.length} recipes scaled by factor ${scaleFactor}`
      });
    } catch (error) {
      // Return error response in case of failure
      return plainToInstance(ShoppingListResponse, {
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
      return plainToInstance(RecipeInfoResponse, {
        id: recipe.id,
        name: recipe.name,
        servings: recipe.servings,
        timeToPrepare: recipe.timeToPrepare
      });
    }
    
    return null;
  }
}