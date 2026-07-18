import { Controller, Get, Post, Body, Param } from '@nestjs/common';
import { AppService } from './app.service';
import { MealService } from '../../services/Services.NestJS/meal.service';
import { ShoppingService } from '../../services/Services.NestJS/shopping.service';
import { ApiTags, ApiResponse, ApiOperation } from '@nestjs/swagger';

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

  @Post('recipes/find-by-ingredients')
  @ApiOperation({ summary: 'Find meals with given ingredients' })
  @ApiResponse({ status: 200, description: 'Returns matching meals' })
  async findMealsWithIngredients(@Body() query: { query: string }) {
    return await this.mealService.findMealsWithIngredients(query.query);
  }

  @Post('shopping-list/generate')
  @ApiOperation({ summary: 'Generate a shopping list from recipe names' })
  @ApiResponse({ status: 200, description: 'Returns generated shopping list' })
  async generateShoppingList(@Body() recipeNames: { recipe_names: string[]; scale_factor?: number }) {
    const { recipe_names, scale_factor = 1.0 } = recipeNames;
    return await this.shoppingService.generateShoppingList(recipe_names, scale_factor);
  }
}