import { Module } from '@nestjs/common';
import {
  CHROMA_VECTOR_STORE_OPTIONS,
  ChromaVectorStoreOptions,
  createChromaVectorStoreOptions,
} from '../../ai/VectorStores/NestJS/chroma-vector-store-options';
import { ChromaVectorStore } from '../../ai/VectorStores/NestJS/chroma-vector-store';
import { IEmbeddingService } from '../../ai/Embedding.NestJS/embedding.service.interface';
import { IVectorStore } from '../../ai/VectorStores/NestJS/vector-store';
import { ISemanticRecipeMetadataProvider, SemanticSearchService } from '../../ai/SemanticSearch/NestJS/semantic-search.service';
import { EmbeddingServiceModule } from './embedding-service.module';
import { RecipeRepositoryModule } from './recipe-repository.module';
import { RepositorySemanticMetadataProvider } from './repository-semantic-metadata.provider';

@Module({
  imports: [EmbeddingServiceModule, RecipeRepositoryModule],
  providers: [
    {
      provide: CHROMA_VECTOR_STORE_OPTIONS,
      useFactory: createChromaVectorStoreOptions,
    },
    {
      provide: IVectorStore,
      useFactory: (options: ChromaVectorStoreOptions) => new ChromaVectorStore(options),
      inject: [CHROMA_VECTOR_STORE_OPTIONS],
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
  exports: [SemanticSearchService, ISemanticRecipeMetadataProvider],
})
export class SemanticSearchModule {}