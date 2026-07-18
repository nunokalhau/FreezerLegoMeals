import { IsString, ValidateNested } from 'class-validator';
import { Type } from 'class-transformer';
import { Recipe } from './recipe.dto';

export class IngredientSearchResponse {
  @IsString()
  query: string;

  @ValidateNested({ each: true })
  @Type(() => String)
  searchTerms: string[];

  @IsNumber()
  totalRecipesFound: number;

  @ValidateNested({ each: true })
  @Type(() => Recipe)
  recipes: Recipe[];

  @IsString()
  message: string;
}