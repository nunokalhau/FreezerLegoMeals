import { IsString, IsNumber, IsOptional } from 'class-validator';

export class ShoppingListItem {
  @IsString()
  name: string;

  @IsNumber()
  quantity: number;

  @IsString()
  unit: string;

  @IsOptional()
  @IsString()
  category?: string;
}