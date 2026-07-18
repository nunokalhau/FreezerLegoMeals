// Test script to verify the implementation of the services and controllers

import { MealService } from './src/services/Services.NestJS/meal.service';
import { ShoppingService } from './src/services/Services.NestJS/shopping.service';
import { RecipeRepository } from './src/repositories/Repository.NestJS/recipe.repository';

// Mock repository for testing
class MockRecipeRepository implements RecipeRepositoryInterface {
  async getRecipes() {
    return [
      { id: 1, name: 'Chicken Curry', ingredients: ['chicken', 'onion', 'garlic'] },
      { id: 2, name: 'Beef Stir Fry', ingredients: ['beef', 'broccoli', 'soy sauce'] }
    ];
  }

  async getRecipeById(id: number) {
    if (id === 1) {
      return { id: 1, name: 'Chicken Curry', ingredients: ['chicken', 'onion', 'garlic'] };
    } else if (id === 2) {
      return { id: 2, name: 'Beef Stir Fry', ingredients: ['beef', 'broccoli', 'soy sauce'] };
    }
    return null;
  }

  async findRecipesWithIngredients(ingredients: string[]) {
    return [
      { id: 1, name: 'Chicken Curry', ingredients: ['chicken', 'onion', 'garlic'] },
      { id: 2, name: 'Beef Stir Fry', ingredients: ['beef', 'broccoli', 'soy sauce'] }
    ];
  }

  async getCombinations() {
    return [];
  }

  async getCombinationById(id: number) {
    return null;
  }

  async getIngredients() {
    return [];
  }

  async getIngredientByName(name: string) {
    return null;
  }
}

async function testMealService() {
  console.log('Testing MealService...');
  
  const mockRepo = new MockRecipeRepository();
  const mealService = new MealService(mockRepo);

  try {
    // Test getRecipes
    const recipes = await mealService.getRecipes();
    console.log('getRecipes:', recipes.length, 'recipes found');
    
    // Test searchRecipesByIngredients
    const searchedRecipes = await mealService.searchRecipesByIngredients(['chicken']);
    console.log('searchRecipesByIngredients:', searchedRecipes.length, 'recipes found');
    
    // Test getRecipeById
    const recipe = await mealService.getRecipeById(1);
    console.log('getRecipeById(1):', recipe ? recipe.name : 'not found');
    
    // Test findMealsWithIngredients
    const foundMeals = await mealService.findMealsWithIngredients('chicken and onion');
    console.log('findMealsWithIngredients:', foundMeals.query, foundMeals.totalRecipesFound, 'recipes found');
    
    // Test getRecipeDetails (this should work with our current implementation)
    const recipeDetails = await mealService.getRecipeDetails(1);
    console.log('getRecipeDetails(1):', recipeDetails.recipe ? recipeDetails.recipe.name : 'not found');

    console.log('All MealService tests passed!');
  } catch (error) {
    console.error('MealService test failed:', error);
  }
}

async function testShoppingService() {
  console.log('\nTesting ShoppingService...');
  
  const mockRepo = new MockRecipeRepository();
  const shoppingService = new ShoppingService(mockRepo);

  try {
    // Test getRecipeIngredients
    const ingredients = await shoppingService.getRecipeIngredients('1');
    console.log('getRecipeIngredients:', ingredients.length, 'ingredients found');
    
    // Test generateShoppingList
    const shoppingList = await shoppingService.generateShoppingList(['1', '2'], 1.0);
    console.log('generateShoppingList:', shoppingList.recipes.length, 'recipes, ', shoppingList.ingredients.length, 'ingredients');
    
    console.log('All ShoppingService tests passed!');
  } catch (error) {
    console.error('ShoppingService test failed:', error);
  }
}

// Run the tests
testMealService();
testShoppingService();