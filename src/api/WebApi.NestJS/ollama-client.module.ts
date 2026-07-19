import { Module } from '@nestjs/common';
import { AssistantService } from '../../services/Services.NestJS/assistant.service';
import { ASSISTANT_OPTIONS, createAssistantOptions } from '../../services/Services.NestJS/assistant-options';
import { CONVERSATION_STORE_OPTIONS, ConversationStore, createConversationStoreOptions, InMemoryConversationStore } from '../../services/Services.NestJS/conversation-store';
import { OllamaClient } from '../../services/Services.NestJS/ollama.client';
import { createOllamaOptions, OLLAMA_OPTIONS } from '../../services/Services.NestJS/ollama-options';

@Module({
  providers: [
    AssistantService,
    InMemoryConversationStore,
    {
      provide: ConversationStore,
      useExisting: InMemoryConversationStore,
    },
    {
      provide: ASSISTANT_OPTIONS,
      useFactory: createAssistantOptions,
    },
    {
      provide: CONVERSATION_STORE_OPTIONS,
      useFactory: createConversationStoreOptions,
    },
    OllamaClient,
    {
      provide: OLLAMA_OPTIONS,
      useFactory: createOllamaOptions,
    },
  ],
  exports: [AssistantService, InMemoryConversationStore],
})
export class OllamaClientModule {}