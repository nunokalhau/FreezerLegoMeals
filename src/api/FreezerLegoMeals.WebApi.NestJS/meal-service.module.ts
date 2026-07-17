import { Module } from '@nestjs/common';
import { MealService } from '../../services/FreezerLegoMeals.Services.NestJS/meal.service';
import { RecipeRepository } from '../../repositories/FreezerLegoMeals.Repository.NestJS/recipe.repository';

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