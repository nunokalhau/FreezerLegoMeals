import { Module, ValidationPipe } from '@nestjs/common';
import { APP_PIPE } from '@nestjs/core';
import { AppController } from './app.controller';
import { AppService } from './app.service';
import { EmbeddingsController } from './embeddings.controller';
import { EmbeddingServiceModule } from './embedding-service.module';
import { MealServiceModule } from './meal-service.module';
import { OllamaClientModule } from './ollama-client.module';
import { RecipeRepositoryModule } from './recipe-repository.module';
import { SemanticSearchController } from './semantic-search.controller';
import { SemanticSearchModule } from './semantic-search.module';
import { ShoppingServiceModule } from './shopping-service.module';

@Module({
  imports: [EmbeddingServiceModule, MealServiceModule, OllamaClientModule, RecipeRepositoryModule, SemanticSearchModule, ShoppingServiceModule],
  controllers: [AppController, EmbeddingsController, SemanticSearchController],
  providers: [
    AppService,
    {
      provide: APP_PIPE,
      useClass: ValidationPipe,
    },
  ],
})
export class AppModule {}