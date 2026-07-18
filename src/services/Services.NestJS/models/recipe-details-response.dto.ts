import { IsString, IsOptional } from 'class-validator';
import { Recipe } from './recipe.dto';

export class RecipeDetailsResponse {
  @IsOptional()
  @IsString()
  error?: string;

  @IsOptional()
  @IsString()
  query?: string;

  @IsOptional()
  @ValidateNested()
  recipe?: Recipe;

  @IsOptional()
  @IsString()
  message?: string;
}