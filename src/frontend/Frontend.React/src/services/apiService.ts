// src/services/apiService.ts

import { config } from '../config';

export interface Recipe {
  id: number;
  name: string;
  sourcePath: string;
  tags: string;
  servings?: number;
  timeToPrepare?: number;
  prepping: string;
  freezingNotes: string;
  reheatNotes: string;
  combinations: string;
  notes: string;
  recipeIngredients: RecipeIngredient[];
}

export interface RecipeIngredient {
  recipeId: number;
  ingredientId: number;
  amount?: number;
  unit?: string;
  ingredient: Ingredient;
}

export interface Ingredient {
  id: number;
  name: string;
  category: string;
}

export interface AssistantChatRequest {
  message: string;
  conversationId?: string;
}

export interface AssistantChatResponse {
  conversationId: string;
  response: string;
}

export interface ShoppingListResponse {
  recipes: string[];
  totalRecipes: number;
  scaleFactor: number;
  ingredients: ShoppingListItem[];
  message: string;
}

export interface ShoppingListItem {
  name: string;
  quantity: number;
  unit: string;
  category?: string;
}

export interface RecipeDetailsResponse {
  recipe: Recipe;
  message: string;
}

export interface SearchRecipesRequest {
  ingredients: string[];
}

export interface SearchRecipesResponse {
  recipes: Recipe[];
  totalRecipesFound: number;
}

// ApiService class to handle all HTTP communication with the backend
class ApiService {
  private readonly baseUrl: string;
  private readonly timeout: number;

  constructor() {
    this.baseUrl = config.apiUrl;
    this.timeout = config.apiTimeout;
  }

  private async request<T>(
    endpoint: string,
    options: RequestInit = {}
  ): Promise<T> {
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), this.timeout);

    try {
      const response = await fetch(`${this.baseUrl}${endpoint}`, {
        ...options,
        signal: controller.signal,
      });

      clearTimeout(timeoutId);

      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }

      return await response.json();
    } catch (error) {
      clearTimeout(timeoutId);
      
      if (error instanceof Error && error.name === 'AbortError') {
        throw new Error('Request timeout');
      }
      
      throw error;
    }
  }

  // Recipes endpoints
  async getAllRecipes(): Promise<Recipe[]> {
    return this.request<Recipe[]>('/api/recipes');
  }

  async searchRecipesByIngredients(ingredients: string[]): Promise<SearchRecipesResponse> {
    return this.request<SearchRecipesResponse>('/api/recipes/search', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ ingredients }),
    });
  }

  async getRecipeById(id: number): Promise<Recipe> {
    return this.request<Recipe>(`/api/recipes/${id}`);
  }

  async getRecipeDetails(id: number): Promise<RecipeDetailsResponse> {
    return this.request<RecipeDetailsResponse>(`/api/recipes/${id}/details`);
  }

  // Assistant endpoint
  async chatWithAssistant(request: AssistantChatRequest): Promise<AssistantChatResponse> {
    return this.request<AssistantChatResponse>('/api/assistant/chat', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(request),
    });
  }

  // Shopping list endpoints
  async generateShoppingList(
    recipeIdentifiers: string[],
    scaleFactor: number = 1.0,
    groupByCategory: boolean = true
  ): Promise<ShoppingListResponse> {
    return this.request<ShoppingListResponse>('/api/shopping/generate', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({
        recipeIdentifiers,
        scaleFactor,
        groupByCategory
      }),
    });
  }

  async getRecipeInfo(identifier: string): Promise<Recipe> {
    return this.request<Recipe>(`/api/shopping/${identifier}/info`);
  }
}

export const apiService = new ApiService();