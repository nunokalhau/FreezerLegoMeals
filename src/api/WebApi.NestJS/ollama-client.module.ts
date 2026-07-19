import { Module } from '@nestjs/common';
import { AssistantService } from '../../services/Services.NestJS/assistant.service';
import { OllamaClient } from '../../services/Services.NestJS/ollama.client';
import { createOllamaOptions, OLLAMA_OPTIONS } from '../../services/Services.NestJS/ollama-options';

@Module({
  providers: [
    AssistantService,
    OllamaClient,
    {
      provide: OLLAMA_OPTIONS,
      useFactory: createOllamaOptions,
    },
  ],
  exports: [AssistantService],
})
export class OllamaClientModule {}