import { Module } from '@nestjs/common';
import { AppController } from './app.controller';
import { AppService } from './app.service';
import { MealServiceModule } from './meal-service.module';
import { RecipeRepositoryModule } from './recipe-repository.module';

@Module({
  imports: [MealServiceModule, RecipeRepositoryModule],
  controllers: [AppController],
  providers: [AppService],
})
export class AppModule {}