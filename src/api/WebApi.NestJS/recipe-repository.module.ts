import { Module } from '@nestjs/common';
import { RecipeRepository } from '../../repositories/Repository.NestJS/recipe.repository';

@Module({
  providers: [RecipeRepository],
  exports: [RecipeRepository],
})
export class RecipeRepositoryModule {}