import { Test, TestingModule } from '@nestjs/testing';
import { INestApplication } from '@nestjs/common';
import request from 'supertest';
import { AppController } from '../../../api/WebApi.NestJS/app.controller';
import { AppService } from '../../../api/WebApi.NestJS/app.service';
import { AssistantService } from '../../../services/Services.NestJS/assistant.service';
import { MealService } from '../../../services/Services.NestJS/meal.service';
import { ShoppingService } from '../../../services/Services.NestJS/shopping.service';

describe('Recipes and Shopping Endpoints (Integration)', () => {
  let app: INestApplication;

  const mealServiceMock = {
    getRecipes: jest.fn().mockResolvedValue([{ id: 1, name: 'Spicy Chicken' }]),
    searchRecipesByIngredients: jest.fn().mockResolvedValue([{ id: 1, name: 'Spicy Chicken' }]),
    getRecipeById: jest.fn((id: number) => Promise.resolve(id === 1 ? { id: 1, name: 'Spicy Chicken' } : null)),
    getRecipeDetails: jest.fn((id: number) => Promise.resolve(id === 1
      ? { recipe: { id: 1, name: 'Spicy Chicken' }, message: 'ok' }
      : { error: 'not found' })),
    findMealsWithIngredients: jest.fn().mockResolvedValue({
      query: 'chicken',
      searchTerms: ['chicken'],
      totalRecipesFound: 1,
      recipes: [{ id: 1, name: 'Spicy Chicken' }],
      message: 'Found 1 recipe',
    }),
  };

  const shoppingServiceMock = {
    generateShoppingList: jest.fn().mockResolvedValue({ message: 'Shopping list generated', items: [] }),
    getRecipeIngredients: jest.fn((identifier: string) => Promise.resolve(identifier === '1' ? [{ ingredientId: 1, recipeId: 1 }] : [])),
    getMultipleRecipeIngredients: jest.fn().mockResolvedValue({ '1': [{ ingredientId: 1, recipeId: 1 }] }),
    getRecipeInfo: jest.fn((identifier: string) => Promise.resolve(identifier === '1'
      ? { id: 1, name: 'Spicy Chicken', servings: 4, timeToPrepare: 30 }
      : null)),
  };

  beforeAll(async () => {
    const moduleFixture: TestingModule = await Test.createTestingModule({
      controllers: [AppController],
      providers: [
        AppService,
        { provide: AssistantService, useValue: { chat: jest.fn() } },
        { provide: MealService, useValue: mealServiceMock },
        { provide: ShoppingService, useValue: shoppingServiceMock },
      ],
    }).compile();

    app = moduleFixture.createNestApplication();
    await app.init();
  });

  afterAll(async () => {
    await app.close();
  });

  it('POST /api/recipes/search returns matches for valid ingredients', async () => {
    const response = await request(app.getHttpServer())
      .post('/api/recipes/search')
      .set('Content-Type', 'application/json')
      .send(JSON.stringify({ ingredients: ['chicken'] }))
      .expect(201);

    expect(response.body.totalRecipesFound).toBe(1);
    expect(response.body.recipes[0].name).toBe('Spicy Chicken');
  });

  it('POST /api/recipes/search returns 400 for empty ingredients', async () => {
    await request(app.getHttpServer())
      .post('/api/recipes/search')
      .set('Content-Type', 'application/json')
      .send(JSON.stringify({ ingredients: [] }))
      .expect(400);
  });

  it('GET /api/recipes/:id returns recipe when found and 404 when missing', async () => {
    const found = await request(app.getHttpServer())
      .get('/api/recipes/1')
      .expect(200);

    expect(found.body.recipe.name).toBe('Spicy Chicken');

    await request(app.getHttpServer())
      .get('/api/recipes/999')
      .expect(404);
  });

  it('GET /api/recipes/:id/details returns recipe details when found and 404 when missing', async () => {
    const found = await request(app.getHttpServer())
      .get('/api/recipes/1/details')
      .expect(200);

    expect(found.body.recipe.name).toBe('Spicy Chicken');

    await request(app.getHttpServer())
      .get('/api/recipes/999/details')
      .expect(404);
  });

  it('POST /api/recipes/find-by-ingredients validates query and returns matches', async () => {
    const found = await request(app.getHttpServer())
      .post('/api/recipes/find-by-ingredients')
      .set('Content-Type', 'application/json')
      .send(JSON.stringify({ query: 'chicken' }))
      .expect(201);

    expect(found.body.totalRecipesFound).toBe(1);

    await request(app.getHttpServer())
      .post('/api/recipes/find-by-ingredients')
      .set('Content-Type', 'application/json')
      .send(JSON.stringify({ query: ' ' }))
      .expect(400);
  });

  it('POST /api/shopping/generate validates request and returns generated list', async () => {
    const response = await request(app.getHttpServer())
      .post('/api/shopping/generate')
      .set('Content-Type', 'application/json')
      .send(JSON.stringify({ recipeIdentifiers: ['1'], scaleFactor: 1.0, groupByCategory: true }))
      .expect(201);

    expect(response.body.shoppingList).toBeDefined();

    await request(app.getHttpServer())
      .post('/api/shopping/generate')
      .set('Content-Type', 'application/json')
      .send(JSON.stringify({ recipeIdentifiers: [] }))
      .expect(400);
  });

  it('GET /api/shopping/ingredients/:identifier returns ingredients', async () => {
    const found = await request(app.getHttpServer())
      .get('/api/shopping/ingredients/1')
      .expect(200);

    expect(found.body.found).toBe(true);

    const missing = await request(app.getHttpServer())
      .get('/api/shopping/ingredients/999')
      .expect(200);

    expect(missing.body.found).toBe(false);
  });

  it('POST /api/shopping/ingredients validates body and returns aggregate results', async () => {
    const response = await request(app.getHttpServer())
      .post('/api/shopping/ingredients')
      .set('Content-Type', 'application/json')
      .send(JSON.stringify(['1']))
      .expect(201);

    expect(response.body.totalRecipes).toBeGreaterThan(0);

    await request(app.getHttpServer())
      .post('/api/shopping/ingredients')
      .set('Content-Type', 'application/json')
      .send(JSON.stringify([]))
      .expect(400);
  });

  it('GET /api/shopping/:identifier/info returns info when found and 404 when missing', async () => {
    const found = await request(app.getHttpServer())
      .get('/api/shopping/1/info')
      .expect(200);

    expect(found.body.info.name).toBe('Spicy Chicken');

    await request(app.getHttpServer())
      .get('/api/shopping/999/info')
      .expect(404);
  });
});
