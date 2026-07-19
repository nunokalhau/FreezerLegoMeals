import { Module, ValidationPipe } from '@nestjs/common';
import { APP_PIPE } from '@nestjs/core';
import { AppController } from './app.controller';
import { AppService } from './app.service';
import { MealServiceModule } from './meal-service.module';
import { RecipeRepositoryModule } from './recipe-repository.module';
import { ShoppingServiceModule } from './shopping-service.module';

@Module({
  imports: [MealServiceModule, RecipeRepositoryModule, ShoppingServiceModule],
  controllers: [AppController],
  providers: [
    AppService,
    {
      provide: APP_PIPE,
      useClass: ValidationPipe,
    },
  ],
})
export class AppModule {}