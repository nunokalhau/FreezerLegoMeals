import { IsNumber, IsString, IsOptional } from 'class-validator';

export class RecipeInfoResponse {
  @IsNumber()
  id: number;

  @IsString()
  name: string;

  @IsNumber()
  servings: number;

  @IsNumber()
  timeToPrepare: number;

  @IsOptional()
  @IsString()
  error?: string;
}