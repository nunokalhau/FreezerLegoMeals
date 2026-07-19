import { BadRequestException, NotFoundException } from '@nestjs/common';
import { AppController } from '../../../api/WebApi.NestJS/app.controller';
import { AppService } from '../../../api/WebApi.NestJS/app.service';
import { AssistantService } from '../../../services/Services.NestJS/assistant.service';
import { MealService } from '../../../services/Services.NestJS/meal.service';
import { ShoppingService } from '../../../services/Services.NestJS/shopping.service';

describe('AppController functional behavior', () => {
  let controller: AppController;
  let assistantService: jest.Mocked<AssistantService>;
  let mealService: jest.Mocked<MealService>;
  let shoppingService: jest.Mocked<ShoppingService>;

  beforeEach(() => {
    const appService = { getHello: () => 'hello' } as AppService;
    assistantService = {
      chat: jest.fn(),
    } as unknown as jest.Mocked<AssistantService>;

    mealService = {
      getRecipes: jest.fn(),
      searchRecipesByIngredients: jest.fn(),
      getRecipeById: jest.fn(),
      getRecipeDetails: jest.fn(),
      findMealsWithIngredients: jest.fn(),
    } as unknown as jest.Mocked<MealService>;

    shoppingService = {
      getRecipeIngredients: jest.fn(),
      getMultipleRecipeIngredients: jest.fn(),
      generateShoppingList: jest.fn(),
      getRecipeInfo: jest.fn(),
    } as unknown as jest.Mocked<ShoppingService>;

    controller = new AppController(appService, assistantService, mealService, shoppingService);
  });

  it('searchRecipesByIngredients validates empty input', async () => {
    await expect(controller.searchRecipesByIngredients({ ingredients: [] })).rejects.toBeInstanceOf(BadRequestException);
  });

  it('searchRecipesByIngredients returns wrapped result', async () => {
    mealService.searchRecipesByIngredients.mockResolvedValue([{ id: 1, name: 'A' }] as any);

    const result = await controller.searchRecipesByIngredients({ ingredients: ['chicken'] });

    expect(result.totalRecipesFound).toBe(1);
    expect(result.recipes[0].name).toBe('A');
  });

  it('getRecipeById throws when missing', async () => {
    mealService.getRecipeById.mockResolvedValue(null);

    await expect(controller.getRecipeById(99)).rejects.toBeInstanceOf(NotFoundException);
  });

  it('generateShoppingList supports snake_case request fields', async () => {
    shoppingService.generateShoppingList.mockResolvedValue({ message: 'ok' } as any);

    const result = await controller.generateShoppingList({
      recipe_identifiers: ['r1'],
      scale_factor: 2,
      group_by_category: false,
    });

    expect(shoppingService.generateShoppingList).toHaveBeenCalledWith(['r1'], 2, false);
    expect(result.scaleFactor).toBe(2);
    expect(result.groupByCategory).toBe(false);
  });

  it('getMultipleRecipeIngredients validates body', async () => {
    await expect(controller.getMultipleRecipeIngredients({ recipeIdentifiers: [] })).rejects.toBeInstanceOf(BadRequestException);
  });

  it('getRecipeInfo throws not found when service returns null', async () => {
    shoppingService.getRecipeInfo.mockResolvedValue(null);

    await expect(controller.getRecipeInfo('abc')).rejects.toBeInstanceOf(NotFoundException);
  });

  it('chatWithAssistant delegates to AssistantService', async () => {
    assistantService.chat.mockResolvedValue({ conversationId: 'conversation-1', response: 'assistant response' });

    const result = await controller.chatWithAssistant({ message: 'Hello', conversationId: 'conversation-1' });

    expect(assistantService.chat).toHaveBeenCalledWith('Hello', 'conversation-1');
    expect(result.conversationId).toBe('conversation-1');
    expect(result.response).toBe('assistant response');
  });

  it('chatWithAssistant validates empty messages', async () => {
    await expect(controller.chatWithAssistant({ message: ' ' })).rejects.toBeInstanceOf(BadRequestException);
  });
});
