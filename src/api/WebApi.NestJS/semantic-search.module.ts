import { Module } from '@nestjs/common';
import { resolve } from 'path';
import { IEmbeddingService } from '../../ai/Embedding.NestJS/embedding.service.interface';
import { IVectorStore, LocalVectorStore } from '../../ai/VectorStores/NestJS/local-vector-store';
import { ISemanticRecipeMetadataProvider, SemanticSearchService } from '../../ai/SemanticSearch/NestJS/semantic-search.service';
import { EmbeddingServiceModule } from './embedding-service.module';
import { RecipeRepositoryModule } from './recipe-repository.module';
import { RepositorySemanticMetadataProvider } from './repository-semantic-metadata.provider';

@Module({
  imports: [EmbeddingServiceModule, RecipeRepositoryModule],
  providers: [
    {
      provide: IVectorStore,
      useFactory: () => new LocalVectorStore(resolve(process.cwd(), '..', '..', '..', 'data', 'embeddings')),
    },
    {
      provide: ISemanticRecipeMetadataProvider,
      useClass: RepositorySemanticMetadataProvider,
    },
    {
      provide: SemanticSearchService,
      useFactory: (
        embeddingService: IEmbeddingService,
        vectorStore: IVectorStore,
        metadataProvider: ISemanticRecipeMetadataProvider
      ) => new SemanticSearchService(embeddingService, vectorStore, metadataProvider),
      inject: [IEmbeddingService, IVectorStore, ISemanticRecipeMetadataProvider],
    },
  ],
  exports: [SemanticSearchService],
})
export class SemanticSearchModule {}