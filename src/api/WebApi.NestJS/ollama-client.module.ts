import { Module } from '@nestjs/common';
import { resolve } from 'path';
import { PromptBuilder } from '../../ai/RAG/NestJS/prompt-builder';
import { RetrievalService } from '../../ai/RAG/NestJS/retrieval.service';
import { ISemanticRecipeMetadataProvider, SemanticSearchService } from '../../ai/SemanticSearch/NestJS/semantic-search.service';
import { MealPlanningAgent } from '../../orchestration/NestJS/meal-planning.agent';
import { OrchestratorService } from '../../orchestration/NestJS/orchestrator.service';
import { AssistantService } from '../../services/Services.NestJS/assistant.service';
import { ASSISTANT_OPTIONS, createAssistantOptions } from '../../services/Services.NestJS/assistant-options';
import { CONVERSATION_STORE_OPTIONS, ConversationStore, createConversationStoreOptions, InMemoryConversationStore } from '../../services/Services.NestJS/conversation-store';
import { OllamaClient } from '../../services/Services.NestJS/ollama.client';
import { createOllamaOptions, OLLAMA_OPTIONS } from '../../services/Services.NestJS/ollama-options';
import { ToolExecutor } from '../../services/Services.NestJS/tool-executor';
import { ToolRegistry } from '../../services/Services.NestJS/tool-registry';
import { SemanticSearchModule } from './semantic-search.module';

@Module({
  imports: [SemanticSearchModule],
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
      useFactory: (toolRegistry: ToolRegistry) => new ToolExecutor(toolRegistry, resolve(process.cwd(), '..', '..', 'tools')),
      inject: [ToolRegistry],
    },
    {
      provide: RetrievalService,
      useFactory: (semanticSearchService: SemanticSearchService, metadataProvider: ISemanticRecipeMetadataProvider) =>
        new RetrievalService(semanticSearchService, metadataProvider),
      inject: [SemanticSearchService, ISemanticRecipeMetadataProvider],
    },
    {
      provide: PromptBuilder,
      useFactory: () => PromptBuilder.fromFile(resolve(process.cwd(), '..', '..', 'ai', 'RAG', 'prompts', 'rag_prompt.txt')),
    },
    {
      provide: MealPlanningAgent,
      useFactory: (ollamaClient: OllamaClient, toolExecutor: ToolExecutor, retrievalService: RetrievalService, promptBuilder: PromptBuilder) =>
        new MealPlanningAgent(ollamaClient, toolExecutor, retrievalService, promptBuilder),
      inject: [OllamaClient, ToolExecutor, RetrievalService, PromptBuilder],
    },
    {
      provide: OrchestratorService,
      useFactory: (mealPlanningAgent: MealPlanningAgent) => new OrchestratorService([mealPlanningAgent]),
      inject: [MealPlanningAgent],
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