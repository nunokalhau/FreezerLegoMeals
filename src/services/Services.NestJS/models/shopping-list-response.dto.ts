import { IsString, IsNumber, ValidateNested } from 'class-validator';
import { Type } from 'class-transformer';
import { ShoppingListItem } from './shopping-list-item.dto';

export class ShoppingListResponse {
  @ValidateNested({ each: true })
  @Type(() => String)
  recipes: string[];

  @IsNumber()
  totalRecipes: number;

  @IsNumber()
  scaleFactor: number;

  @ValidateNested({ each: true })
  @Type(() => ShoppingListItem)
  ingredients: ShoppingListItem[];

  @IsString()
  message: string;
}