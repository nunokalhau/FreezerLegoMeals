import { IsArray, IsString, ArrayNotEmpty } from 'class-validator';

export class ShoppingIngredientsRequest {
  @IsArray()
  @ArrayNotEmpty()
  recipeIdentifiers: string[];
}