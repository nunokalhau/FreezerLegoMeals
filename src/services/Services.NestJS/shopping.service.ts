import { Injectable } from '@nestjs/common';
import { IShoppingService } from './shopping.service.interface';
import { RecipeRepositoryInterface } from '../../repositories/Repository.NestJS/recipe.repository';

@Injectable()
export class ShoppingService implements IShoppingService {
  constructor(
    private readonly recipeRepository: RecipeRepositoryInterface
  ) {}

  async getRecipeIngredients(recipeIdentifier: string): Promise<any[]> {
    return [];
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
  ): Promise<any> {
    if (!recipeIdentifiers || recipeIdentifiers.length === 0) {
      return {
        recipes: recipeIdentifiers,
        total_recipes: 0,
        scale_factor: scaleFactor,
        ingredients: [],
        message: "No recipes provided to generate shopping list"
      };
    }

    return {
      recipes: recipeIdentifiers,
      total_recipes: recipeIdentifiers.length,
      scale_factor: scaleFactor,
      ingredients: [],
      message: `Generated shopping list for ${recipeIdentifiers.length} recipes`
    };
  }

  async getRecipeInfo(recipeIdentifier: string): Promise<any | null> {
    const parsedId = parseInt(recipeIdentifier, 10);
    let recipe;
    
    if (!isNaN(parsedId)) {
      recipe = await this.recipeRepository.getRecipeById(parsedId);
    } else {
      recipe = null; 
    }
    
    if (recipe) {
      return {
        id: recipe.id,
        name: recipe.name,
        servings: recipe.servings,
        time_to_prepare: recipe.timeToPrepare
      };
    }
    
    return null;
  }
}