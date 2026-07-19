import { Controller, Get, Post, Body, Param } from '@nestjs/common';
import { AppService } from './app.service';
import { MealService } from '../../services/Services.NestJS/meal.service';
import { ShoppingService } from '../../services/Services.NestJS/shopping.service';
import { ApiTags, ApiResponse, ApiOperation } from '@nestjs/swagger';
import { ShoppingIngredientsRequest } from '../../services/Services.NestJS/models/shopping-ingredients-request.dto';
import { ValidateDtoPipe } from '../../pipes/validation.pipe';

@ApiTags('meals')
@Controller('api')
export class AppController {
  constructor(
    private readonly appService: AppService,
    private readonly mealService: MealService,
    private readonly shoppingService: ShoppingService
  ) {}

  @Get()
  @ApiOperation({ summary: 'Get the welcome message' })
  @ApiResponse({ status: 200, description: 'Returns a welcome message' })
  getHello(): string {
    return this.appService.getHello();
  }

  @Get('health')
  @ApiOperation({ summary: 'Health check endpoint' })
  @ApiResponse({ status: 200, description: 'Service is healthy' })
  healthCheck() {
    return {
      status: 'healthy',
      service: 'WebApi.NestJS'
    };
  }

  @Get('recipes')
  @ApiOperation({ summary: 'Get all recipes' })
  @ApiResponse({ status: 200, description: 'Returns all recipes' })
  async getAllRecipes() {
    return await this.mealService.getRecipes();
  }

  @Get('recipes/search')
  @ApiOperation({ summary: 'Search recipes by ingredients' })
  @ApiResponse({ status: 200, description: 'Returns matching recipes' })
  async searchRecipesByIngredients(@Body() ingredients: string[]) {
    return await this.mealService.searchRecipesByIngredients(ingredients);
  }

  @Get('recipes/:id')
  @ApiOperation({ summary: 'Get a recipe by ID' })
  @ApiResponse({ status: 200, description: 'Returns the recipe details' })
  async getRecipeById(@Param('id') id: number) {
    return await this.mealService.getRecipeById(id);
  }

  @Get('recipes/:id/details')
  @ApiOperation({ summary: 'Get detailed recipe information by ID' })
  @ApiResponse({ status: 200, description: 'Returns the recipe details' })
  async getRecipeDetails(@Param('id') id: number) {
    return await this.mealService.getRecipeDetails(id);
  }

  @Post('recipes/find-by-ingredients')
  @ApiOperation({ summary: 'Find meals with given ingredients' })
  @ApiResponse({ status: 200, description: 'Returns matching meals' })
  async findMealsWithIngredients(@Body() query: { query: string }) {
    return await this.mealService.findMealsWithIngredients(query.query);
  }

  @Post('shopping/generate')
  @ApiOperation({ summary: 'Generate a shopping list from recipe names' })
  @ApiResponse({ status: 200, description: 'Returns generated shopping list' })
  async generateShoppingList(@Body() recipeNames: { recipe_names: string[]; scale_factor?: number }) {
    const { recipe_names, scale_factor = 1.0 } = recipeNames;
    return await this.shoppingService.generateShoppingList(recipe_names, scale_factor);
  }

  @Get('shopping/:recipeName')
  @ApiOperation({ summary: 'Get ingredients for a specific recipe' })
  @ApiResponse({ status: 200, description: 'Returns recipe ingredients' })
  async getRecipeIngredients(@Param('recipeName') recipeName: string) {
    return await this.shoppingService.getRecipeIngredients(recipeName);
  }

  @Post('shopping/ingredients')
  @ApiOperation({ summary: 'Get ingredients for multiple recipes' })
  @ApiResponse({ status: 200, description: 'Returns ingredients for multiple recipes' })
  async getMultipleRecipeIngredients(
    @Body() 
    request: ShoppingIngredientsRequest
  ) {
    return await this.shoppingService.getMultipleRecipeIngredients(request.recipeIdentifiers);
  }

  @Get('shopping/:identifier/info')
  @ApiOperation({ summary: 'Get basic information for a specific recipe' })
  @ApiResponse({ status: 200, description: 'Returns recipe information' })
  async getRecipeInfo(@Param('identifier') identifier: string) {
    return await this.shoppingService.getRecipeInfo(identifier);
  }
}