import { Module } from '@nestjs/common';
import { ShoppingService } from '../../services/Services.NestJS/shopping.service';
import { RecipeRepository } from '../../repositories/Repository.NestJS/recipe.repository';

@Module({
  providers: [
    ShoppingService,
    {
      provide: 'RecipeRepositoryInterface',
      useClass: RecipeRepository,
    },
  ],
  exports: [ShoppingService],
})
export class ShoppingServiceModule {}