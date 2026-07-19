import { Injectable } from '@nestjs/common';
import { BaseRepository } from './base.repository';
import { Recipe } from '../../services/Services.NestJS/models/recipe.dto';
import * as path from 'path';
import * as fs from 'fs';
import sqlite3 from 'sqlite3';

type RecipeRow = {
  id: number;
  name: string;
  source_path: string;
  tags: string;
  servings: number | null;
  time_to_prepare: number | null;
  prepping: string;
  freezing_notes: string;
  reheat_notes: string;
  combinations: string;
  notes: string;
};

type RecipeIngredientRow = {
  id: number;
  recipe_id: number;
  ingredient_id: number;
  amount: number | null;
  unit: string | null;
  ingredient_name: string;
};

export interface RecipeRepositoryInterface {
  getRecipes(): Promise<Recipe[]>;
  getRecipeById(id: number): Promise<Recipe | null>;
  findRecipesWithIngredients(ingredients: string[]): Promise<Recipe[]>;
  getCombinations(): Promise<any[]>;
  getCombinationById(id: number): Promise<any | null>;
  getIngredients(): Promise<any[]>;
  getIngredientByName(name: string): Promise<any | null>;
}

@Injectable()
export class RecipeRepository extends BaseRepository implements RecipeRepositoryInterface {
  private readonly dbPath = path.resolve(process.cwd(), 'data', 'recipes_local.db');

  constructor() {
    super();
    this.getRecipes = this.getRecipes.bind(this);
    this.getRecipeById = this.getRecipeById.bind(this);
    this.findRecipesWithIngredients = this.findRecipesWithIngredients.bind(this);
    this.getCombinations = this.getCombinations.bind(this);
    this.getCombinationById = this.getCombinationById.bind(this);
    this.getIngredients = this.getIngredients.bind(this);
    this.getIngredientByName = this.getIngredientByName.bind(this);
  }

  private hasDatabase(): boolean {
    return fs.existsSync(this.dbPath);
  }

  private async queryAll<T>(query: string, params: unknown[] = []): Promise<T[]> {
    return new Promise((resolve, reject) => {
      const db = new sqlite3.Database(this.dbPath, sqlite3.OPEN_READONLY, (openError) => {
        if (openError) {
          reject(openError);
        }
      });

      db.all(query, params, (error, rows) => {
        db.close();
        if (error) {
          reject(error);
          return;
        }

        resolve((rows || []) as T[]);
      });
    });
  }

  private async queryOne<T>(query: string, params: unknown[] = []): Promise<T | null> {
    return new Promise((resolve, reject) => {
      const db = new sqlite3.Database(this.dbPath, sqlite3.OPEN_READONLY, (openError) => {
        if (openError) {
          reject(openError);
        }
      });

      db.get(query, params, (error, row) => {
        db.close();
        if (error) {
          reject(error);
          return;
        }

        resolve((row as T) || null);
      });
    });
  }

  private async getRecipeIngredientsByRecipeId(recipeId: number) {
    const rows = await this.queryAll<RecipeIngredientRow>(
      `
      SELECT
        ri.id,
        ri.recipe_id,
        ri.ingredient_id,
        ri.amount,
        ri.unit,
        i.name AS ingredient_name
      FROM recipe_ingredients ri
      JOIN ingredients i ON i.id = ri.ingredient_id
      WHERE ri.recipe_id = ?
      ORDER BY i.name
      `,
      [recipeId]
    );

    return rows.map((row) => ({
      recipeId: row.recipe_id,
      ingredientId: row.ingredient_id,
      amount: row.amount ?? undefined,
      unit: row.unit ?? undefined,
      ingredient: {
        id: row.ingredient_id,
        name: row.ingredient_name,
        category: 'other'
      }
    }));
  }

  private async mapRecipe(row: RecipeRow): Promise<Recipe> {
    const recipeIngredients = await this.getRecipeIngredientsByRecipeId(row.id);

    return {
      id: row.id,
      name: row.name,
      sourcePath: row.source_path || '',
      tags: row.tags || '',
      servings: row.servings ?? undefined,
      timeToPrepare: row.time_to_prepare ?? undefined,
      prepping: row.prepping || '',
      freezingNotes: row.freezing_notes || '',
      reheatNotes: row.reheat_notes || '',
      combinations: row.combinations || '',
      notes: row.notes || '',
      recipeIngredients
    };
  }

  async getRecipes(): Promise<Recipe[]> {
    if (!this.hasDatabase()) {
      return [];
    }

    try {
      const rows = await this.queryAll<RecipeRow>(
        `
        SELECT
          id,
          name,
          source_path,
          tags,
          servings,
          time_to_prepare,
          prepping,
          freezing_notes,
          reheat_notes,
          combinations,
          notes
        FROM recipes
        ORDER BY name
        `
      );

      const recipes: Recipe[] = [];
      for (const row of rows) {
        recipes.push(await this.mapRecipe(row));
      }

      return recipes;
    } catch (error) {
      this.handleError(error, 'get recipes');
      return [];
    }
  }

  async getRecipeById(id: number): Promise<Recipe | null> {
    if (!this.hasDatabase() || id <= 0) {
      return null;
    }

    try {
      const row = await this.queryOne<RecipeRow>(
        `
        SELECT
          id,
          name,
          source_path,
          tags,
          servings,
          time_to_prepare,
          prepping,
          freezing_notes,
          reheat_notes,
          combinations,
          notes
        FROM recipes
        WHERE id = ?
        `,
        [id]
      );

      if (!row) {
        return null;
      }

      return await this.mapRecipe(row);
    } catch (error) {
      this.handleError(error, 'get recipe by id');
      return null;
    }
  }

  async findRecipesWithIngredients(ingredients: string[]): Promise<Recipe[]> {
    if (!this.hasDatabase() || !Array.isArray(ingredients) || ingredients.length === 0) {
      return [];
    }

    const normalizedIngredients = ingredients
      .map((ingredient) => ingredient?.trim().toLowerCase())
      .filter((ingredient) => !!ingredient);

    if (normalizedIngredients.length === 0) {
      return [];
    }

    try {
      const placeholders = normalizedIngredients.map(() => '?').join(', ');
      const rows = await this.queryAll<RecipeRow>(
        `
        SELECT DISTINCT
          r.id,
          r.name,
          r.source_path,
          r.tags,
          r.servings,
          r.time_to_prepare,
          r.prepping,
          r.freezing_notes,
          r.reheat_notes,
          r.combinations,
          r.notes
        FROM recipes r
        JOIN recipe_ingredients ri ON ri.recipe_id = r.id
        JOIN ingredients i ON i.id = ri.ingredient_id
        WHERE LOWER(i.name) IN (${placeholders})
        ORDER BY r.name
        `,
        normalizedIngredients
      );

      const recipes: Recipe[] = [];
      for (const row of rows) {
        recipes.push(await this.mapRecipe(row));
      }

      return recipes;
    } catch (error) {
      this.handleError(error, 'find recipes with ingredients');
      return [];
    }
  }

  async getCombinations(): Promise<any[]> {
    if (!this.hasDatabase()) {
      return [];
    }

    try {
      const combinations = await this.queryAll<{ id: number; name: string; description: string }>(
        'SELECT id, name, description FROM recipe_combinations ORDER BY name'
      );

      const items = await this.queryAll<{
        combination_id: number;
        recipe_id: number;
        position: number;
        recipe_name: string;
      }>(
        `
        SELECT
          rci.combination_id,
          rci.recipe_id,
          rci.position,
          r.name AS recipe_name
        FROM recipe_combination_items rci
        JOIN recipes r ON r.id = rci.recipe_id
        ORDER BY rci.combination_id, rci.position
        `
      );

      return combinations.map((combination) => ({
        id: combination.id,
        name: combination.name,
        description: combination.description,
        recipes: items
          .filter((item) => item.combination_id === combination.id)
          .map((item) => ({
            id: item.recipe_id,
            name: item.recipe_name,
            position: item.position
          }))
      }));
    } catch (error) {
      this.handleError(error, 'get combinations');
      return [];
    }
  }

  async getCombinationById(id: number): Promise<any | null> {
    const combinations = await this.getCombinations();
    return combinations.find((combination) => combination.id === id) || null;
  }

  async getIngredients(): Promise<any[]> {
    if (!this.hasDatabase()) {
      return [];
    }

    try {
      return await this.queryAll<{ id: number; name: string }>(
        'SELECT id, name FROM ingredients ORDER BY name'
      );
    } catch (error) {
      this.handleError(error, 'get ingredients');
      return [];
    }
  }

  async getIngredientByName(name: string): Promise<any | null> {
    if (!this.hasDatabase() || !name || !name.trim()) {
      return null;
    }

    try {
      return await this.queryOne<{ id: number; name: string }>(
        'SELECT id, name FROM ingredients WHERE LOWER(name) = LOWER(?) LIMIT 1',
        [name.trim()]
      );
    } catch (error) {
      this.handleError(error, 'get ingredient by name');
      return null;
    }
  }
}