import { IsNumber, IsString, IsOptional, ValidateNested } from 'class-validator';
import { Type } from 'class-transformer';

export class Ingredient {
  @IsNumber()
  id: number;

  @IsString()
  name: string;

  @IsString()
  category: string;
}

export class RecipeIngredient {
  @IsNumber()
  recipeId: number;

  @IsNumber()
  ingredientId: number;

  @IsOptional()
  @IsNumber()
  amount?: number;

  @IsOptional()
  @IsString()
  unit?: string;

  @ValidateNested()
  @Type(() => Ingredient)
  ingredient: Ingredient;
}

export class Recipe {
  @IsNumber()
  id: number;

  @IsString()
  name: string;

  @IsString()
  sourcePath: string;

  @IsString()
  tags: string;

  @IsOptional()
  @IsNumber()
  servings?: number;

  @IsOptional()
  @IsNumber()
  timeToPrepare?: number;

  @IsString()
  prepping: string;

  @IsString()
  freezingNotes: string;

  @IsString()
  reheatNotes: string;

  @IsString()
  combinations: string;

  @IsString()
  notes: string;

  @ValidateNested({ each: true })
  @Type(() => RecipeIngredient)
  recipeIngredients: RecipeIngredient[];
}