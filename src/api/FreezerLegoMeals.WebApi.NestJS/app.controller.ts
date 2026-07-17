import { Controller, Get, Post, Body, Param } from '@nestjs/common';
import { AppService } from './app.service';
import { MealService } from '../../services/FreezerLegoMeals.Services.NestJS/meal.service';
import { ShoppingService } from '../../services/FreezerLegoMeals.Services.NestJS/shopping.service';

@Controller('api')
export class AppController {
  constructor(
    private readonly appService: AppService,
    private readonly mealService: MealService,
    private readonly shoppingService: ShoppingService
  ) {}

  @Get()
  getHello(): string {
    return this.appService.getHello();
  }

  @Get('health')
  healthCheck() {
    return {
      status: 'healthy',
      service: 'FreezerLegoMeals.WebApi.NestJS'
    };
  }

  @Get('recipes/search')
  async searchRecipesByIngredients(@Body() ingredients: string[]) {
    return await this.mealService.searchRecipesByIngredients(ingredients);
  }

  @Get('recipes/:id')
  async getRecipeById(@Param('id') id: number) {
    return await this.mealService.getRecipeById(id);
  }

  @Post('recipes/find-by-ingredients')
  async findMealsWithIngredients(@Body() query: { query: string }) {
    return await this.mealService.findMealsWithIngredients(query.query);
  }

  @Post('shopping-list/generate')
  async generateShoppingList(@Body() recipeNames: { recipe_names: string[]; scale_factor?: number }) {
    const { recipe_names, scale_factor = 1.0 } = recipeNames;
    return await this.shoppingService.generateShoppingList(recipe_names, scale_factor);
  }
}