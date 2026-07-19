import { Inject, Injectable } from '@nestjs/common';
import { IShoppingService } from './shopping.service.interface';
import { RecipeRepositoryInterface } from '../../repositories/Repository.NestJS/recipe.repository';
import { ShoppingListResponse } from './models/shopping-list-response.dto';
import { RecipeInfoResponse } from './models/recipe-info-response.dto';
import { ShoppingListItem } from './models/shopping-list-item.dto';
import { Recipe, RecipeIngredient } from './models/recipe.dto';
import { plainToInstance } from 'class-transformer';

@Injectable()
export class ShoppingService implements IShoppingService {
  constructor(
    @Inject('RecipeRepositoryInterface')
    private readonly recipeRepository: RecipeRepositoryInterface
  ) {}

  async getRecipeIngredients(recipeIdentifier: string): Promise<RecipeIngredient[]> {
    if (!recipeIdentifier || !recipeIdentifier.trim()) {
      return [];
    }

    const parsedId = parseInt(recipeIdentifier, 10);
    let recipe: Recipe | null = null;
    
    if (!isNaN(parsedId)) {
      recipe = await this.recipeRepository.getRecipeById(parsedId);
    } else {
      const recipes = await this.recipeRepository.findRecipesWithIngredients([recipeIdentifier]);
      recipe = recipes.length > 0 ? recipes[0] : null;
    }

    if (!recipe) {
      return [];
    }
    
    return recipe.recipeIngredients || [];
  }

  async getMultipleRecipeIngredients(recipeIdentifiers: string[]): Promise<Record<string, RecipeIngredient[]>> {
    const result: Record<string, RecipeIngredient[]> = {};
    
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
    if (scaleFactor <= 0) {
      return plainToInstance(ShoppingListResponse, {
        recipes: recipeIdentifiers || [],
        totalRecipes: recipeIdentifiers?.length || 0,
        scaleFactor,
        ingredients: [],
        message: 'Scale factor must be greater than 0'
      });
    }

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
      // Get all recipe ingredients to build an aggregate shopping list.
      const allIngredientsByRecipe = await this.getMultipleRecipeIngredients(recipeIdentifiers);
      for (const identifier of recipeIdentifiers) {
        if (!allIngredientsByRecipe[identifier]) {
          allIngredientsByRecipe[identifier] = [];
        }
      }

      const ingredientMap: Record<string, ShoppingListItem> = {};
      
      for (const ingredients of Object.values(allIngredientsByRecipe)) {
        if (Array.isArray(ingredients)) {
          for (const recipeIngredient of ingredients) {
            const ingredientName = recipeIngredient.ingredient?.name || 'Unknown ingredient';
            const ingredientCategory = recipeIngredient.ingredient?.category || 'other';
            const ingredientKey = `${ingredientName}-${ingredientCategory}`;
            
            if (!ingredientMap[ingredientKey]) {
              ingredientMap[ingredientKey] = {
                name: ingredientName,
                quantity: 0,
                unit: recipeIngredient.unit || 'units',
                category: ingredientCategory
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
    if (!recipeIdentifier || !recipeIdentifier.trim()) {
      return null;
    }

    const parsedId = parseInt(recipeIdentifier, 10);
    let recipe: Recipe | null = null;
    
    if (!isNaN(parsedId)) {
      recipe = await this.recipeRepository.getRecipeById(parsedId);
    } else {
      const recipes = await this.recipeRepository.findRecipesWithIngredients([recipeIdentifier]);
      recipe = recipes.length > 0 ? recipes[0] : null;
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