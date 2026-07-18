import { Module } from '@nestjs/common';
import { MealService } from '../../services/Services.NestJS/meal.service';
import { RecipeRepository } from '../../repositories/Repository.NestJS/recipe.repository';

@Module({
  providers: [
    MealService,
    {
      provide: 'RecipeRepositoryInterface',
      useClass: RecipeRepository
    }
  ],
  exports: [MealService],
})
export class MealServiceModule {}