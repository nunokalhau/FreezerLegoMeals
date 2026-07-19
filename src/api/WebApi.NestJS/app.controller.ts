import { Body, Controller, Get, Param, ParseIntPipe, Post, BadRequestException, NotFoundException } from '@nestjs/common';
import { AppService } from './app.service';
import { AssistantService } from '../../services/Services.NestJS/assistant.service';
import { MealService } from '../../services/Services.NestJS/meal.service';
import { ShoppingService } from '../../services/Services.NestJS/shopping.service';
import { ApiTags, ApiResponse, ApiOperation } from '@nestjs/swagger';
import { AssistantChatRequest } from '../../services/Services.NestJS/models/assistant-chat-request.dto';
import { ShoppingIngredientsRequest } from '../../services/Services.NestJS/models/shopping-ingredients-request.dto';

@ApiTags('meals')
@Controller('api')
export class AppController {
  constructor(
    private readonly appService: AppService,
    private readonly assistantService: AssistantService,
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

  @Post('recipes/search')
  @ApiOperation({ summary: 'Search recipes by ingredients' })
  @ApiResponse({ status: 200, description: 'Returns matching recipes' })
  async searchRecipesByIngredients(@Body() body: { ingredients: string[] }) {
    if (!body || !Array.isArray(body.ingredients) || body.ingredients.length === 0) {
      throw new BadRequestException('At least one ingredient is required');
    }

    const recipes = await this.mealService.searchRecipesByIngredients(body.ingredients);
    return {
      recipes,
      totalRecipesFound: recipes.length
    };
  }

  @Get('recipes/:id')
  @ApiOperation({ summary: 'Get a recipe by ID' })
  @ApiResponse({ status: 200, description: 'Returns the recipe details' })
  async getRecipeById(@Param('id', ParseIntPipe) id: number) {
    const recipe = await this.mealService.getRecipeById(id);
    if (!recipe) {
      throw new NotFoundException('Recipe not found');
    }

    return { recipe };
  }

  @Get('recipes/:id/details')
  @ApiOperation({ summary: 'Get detailed recipe information by ID' })
  @ApiResponse({ status: 200, description: 'Returns the recipe details' })
  async getRecipeDetails(@Param('id', ParseIntPipe) id: number) {
    const result = await this.mealService.getRecipeDetails(id);
    if (result.error || !result.recipe) {
      throw new NotFoundException(result.error || 'Recipe details not found');
    }

    return {
      recipe: result.recipe,
      message: result.message || ''
    };
  }

  @Post('recipes/find-by-ingredients')
  @ApiOperation({ summary: 'Find meals with given ingredients' })
  @ApiResponse({ status: 200, description: 'Returns matching meals' })
  async findMealsWithIngredients(@Body() query: { query: string }) {
    if (!query || !query.query || !query.query.trim()) {
      throw new BadRequestException('Query is required');
    }

    return await this.mealService.findMealsWithIngredients(query.query);
  }

  @Post('assistant/chat')
  @ApiOperation({ summary: 'Send a basic chat message to the assistant' })
  @ApiResponse({ status: 200, description: 'Returns the assistant response' })
  async chatWithAssistant(@Body() body: AssistantChatRequest) {
    if (!body || !body.message || !body.message.trim()) {
      throw new BadRequestException('Message is required');
    }

    const response = await this.assistantService.chat(body.message);
    return { response };
  }

  @Post('shopping/generate')
  @ApiOperation({ summary: 'Generate a shopping list from recipe names' })
  @ApiResponse({ status: 200, description: 'Returns generated shopping list' })
  async generateShoppingList(
    @Body()
    body: {
      recipeIdentifiers?: string[];
      scaleFactor?: number;
      groupByCategory?: boolean;
      recipe_identifiers?: string[];
      scale_factor?: number;
      group_by_category?: boolean;
    }
  ) {
    const recipeIdentifiers = body?.recipeIdentifiers ?? body?.recipe_identifiers ?? [];
    const scaleFactor = body?.scaleFactor ?? body?.scale_factor ?? 1.0;
    const groupByCategory = body?.groupByCategory ?? body?.group_by_category ?? true;

    if (!Array.isArray(recipeIdentifiers) || recipeIdentifiers.length === 0) {
      throw new BadRequestException('At least one recipe identifier is required');
    }

    const shoppingList = await this.shoppingService.generateShoppingList(
      recipeIdentifiers,
      scaleFactor,
      groupByCategory
    );

    return {
      shoppingList,
      message: shoppingList.message || 'Shopping list generated successfully',
      scaleFactor,
      groupByCategory
    };
  }

  @Get('shopping/ingredients/:identifier')
  @ApiOperation({ summary: 'Get ingredients for a specific recipe' })
  @ApiResponse({ status: 200, description: 'Returns recipe ingredients' })
  async getRecipeIngredients(@Param('identifier') identifier: string) {
    if (!identifier || !identifier.trim()) {
      throw new BadRequestException('Recipe identifier is required');
    }

    const ingredients = await this.shoppingService.getRecipeIngredients(identifier);
    return {
      ingredients,
      recipeName: identifier,
      found: ingredients.length > 0
    };
  }

  @Post('shopping/ingredients')
  @ApiOperation({ summary: 'Get ingredients for multiple recipes' })
  @ApiResponse({ status: 200, description: 'Returns ingredients for multiple recipes' })
  async getMultipleRecipeIngredients(
    @Body()
    request: ShoppingIngredientsRequest | string[]
  ) {
    const recipeIdentifiers = Array.isArray(request)
      ? request
      : request?.recipeIdentifiers;

    if (!Array.isArray(recipeIdentifiers) || recipeIdentifiers.length === 0) {
      throw new BadRequestException('Request body is required');
    }

    const recipeIngredients = await this.shoppingService.getMultipleRecipeIngredients(recipeIdentifiers);
    const totalRecipes = Object.keys(recipeIngredients).length;
    return {
      recipeIngredients,
      totalRecipes,
      found: totalRecipes > 0
    };
  }

  @Get('shopping/:identifier/info')
  @ApiOperation({ summary: 'Get basic information for a specific recipe' })
  @ApiResponse({ status: 200, description: 'Returns recipe information' })
  async getRecipeInfo(@Param('identifier') identifier: string) {
    if (!identifier || !identifier.trim()) {
      throw new BadRequestException('Recipe identifier is required');
    }

    const info = await this.shoppingService.getRecipeInfo(identifier);
    if (!info) {
      throw new NotFoundException('Recipe info not found');
    }

    return {
      info
    };
  }
}