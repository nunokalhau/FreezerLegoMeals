import { Module } from '@nestjs/common';
import { EMBEDDING_OPTIONS, createEmbeddingOptions } from '../../ai/Embedding.NestJS/embedding-options';
import { IEmbeddingService } from '../../ai/Embedding.NestJS/embedding.service.interface';
import { OllamaEmbeddingService } from '../../ai/Embedding.NestJS/ollama-embedding.service';

@Module({
  providers: [
    {
      provide: EMBEDDING_OPTIONS,
      useFactory: createEmbeddingOptions,
    },
    {
      provide: IEmbeddingService,
      useClass: OllamaEmbeddingService,
    },
  ],
  exports: [IEmbeddingService],
})
export class EmbeddingServiceModule {}