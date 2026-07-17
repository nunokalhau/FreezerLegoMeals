import { Injectable } from '@nestjs/common';
import { IMealService } from './meal.service.interface';
import { RecipeRepositoryInterface } from '../../repositories/FreezerLegoMeals.Repository.NestJS/recipe.repository';

@Injectable()
export class MealService implements IMealService {
  constructor(
    private readonly recipeRepository: RecipeRepositoryInterface
  ) {}

  async searchRecipesByIngredients(ingredients: string[]): Promise<any[]> {
    return await this.recipeRepository.findRecipesWithIngredients(ingredients);
  }

  async getRecipeById(recipeId: number): Promise<any | null> {
    return await this.recipeRepository.getRecipeById(recipeId);
  }

  async findMealsWithIngredients(query: string): Promise<any> {
    // Simple pattern matching for common food terms (as seen in Python version)
    const foodTerms = [
      "chicken", "beef", "pork", "tofu", "rice", "potato", "carrot", 
      "broccoli", "spinach", "onion", "garlic", "tomato", "bean", 
      "pepper", "cucumber", "mushroom", "egg", "salmon", "lamb",
      "turkey", "duck", "shrimp", "fish", "quinoa", "noodles", "pasta"
    ];

    const foundIngredients = [];
    const queryLower = query.toLowerCase();

    for (const term of foodTerms) {
      if (queryLower.includes(term)) {
        foundIngredients.push(term);
      }
    }

    // If no ingredients found, try to extract words that might be ingredients
    if (foundIngredients.length === 0) {
      const words = queryLower.match(/\b\w+\b/g) || [];
      for (const word of words) {
        if (foodTerms.includes(word)) {
          foundIngredients.push(word);
        }
      }
    }

    let recipes: any[] = [];
    if (foundIngredients.length > 0) {
      recipes = await this.recipeRepository.findRecipesWithIngredients(foundIngredients);
    }

    // Return structure matching Python implementation
    return {
      query: query,
      search_terms: foundIngredients,
      total_recipes_found: recipes.length,
      recipes: recipes,
      message: foundIngredients.length > 0 
        ? `Found ${recipes.length} recipes containing ${foundIngredients.join(', ')}`
        : "No ingredients found in your query. Try mentioning specific ingredients like 'chicken', 'beef', etc."
    };
  }

  async getRecipeDetails(recipeId: number): Promise<any> {
    const recipe = await this.recipeRepository.getRecipeById(recipeId);
    
    if (!recipe) {
      return {
        error: `No recipe found with ID ${recipeId}`
      };
    }
    
    return {
      query: `Recipe details for ${recipe.name}`,
      recipe: recipe,
      message: `Details for recipe: ${recipe.name}`
    };
  }
}