import { Module } from '@nestjs/common';
import { resolve } from 'path';
import { AssistantService } from '../../services/Services.NestJS/assistant.service';
import { ASSISTANT_OPTIONS, createAssistantOptions } from '../../services/Services.NestJS/assistant-options';
import { CONVERSATION_STORE_OPTIONS, ConversationStore, createConversationStoreOptions, InMemoryConversationStore } from '../../services/Services.NestJS/conversation-store';
import { OllamaClient } from '../../services/Services.NestJS/ollama.client';
import { createOllamaOptions, OLLAMA_OPTIONS } from '../../services/Services.NestJS/ollama-options';
import { ToolExecutor } from '../../services/Services.NestJS/tool-executor';
import { ToolRegistry } from '../../services/Services.NestJS/tool-registry';

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
    {
      provide: ToolRegistry,
      useFactory: () => new ToolRegistry(resolve(process.cwd(), '..', '..', 'tools', 'tool_registry.json')),
    },
    {
      provide: ToolExecutor,
      useFactory: (toolRegistry: ToolRegistry) => new ToolExecutor(toolRegistry),
      inject: [ToolRegistry],
    },
    OllamaClient,
    {
      provide: OLLAMA_OPTIONS,
      useFactory: createOllamaOptions,
    },
  ],
  exports: [AssistantService, InMemoryConversationStore, ToolExecutor, ToolRegistry],
})
export class OllamaClientModule {}