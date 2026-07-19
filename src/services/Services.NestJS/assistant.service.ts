import { Inject, Injectable, Logger, Optional } from '@nestjs/common';
import { AssistantChatResult, AssistantServiceInterface } from './assistant.service.interface';
import { ASSISTANT_OPTIONS, AssistantOptions, createAssistantOptions } from './assistant-options';
import { ConversationMessage, ConversationStore } from './conversation-store';
import { OllamaClient } from './ollama.client';
import { ToolExecutor } from './tool-executor';
import { PromptBuilder } from '../../ai/RAG/NestJS/prompt-builder';
import { RetrievalService, SourceAttribution } from '../../ai/RAG/NestJS/retrieval.service';

@Injectable()
export class AssistantService implements AssistantServiceInterface {
  private readonly logger = new Logger(AssistantService.name);

  constructor(
    private readonly ollamaClient: OllamaClient,
    @Inject(ConversationStore)
    private readonly conversationStore: ConversationStore,
    private readonly toolExecutor: ToolExecutor,
    @Optional()
    @Inject(ASSISTANT_OPTIONS)
    private readonly options: AssistantOptions = createAssistantOptions(),
    @Optional()
    private readonly retrievalService?: RetrievalService,
    @Optional()
    private readonly promptBuilder?: PromptBuilder
  ) {}

  async chat(message: string, conversationId?: string): Promise<AssistantChatResult> {
    if (!message?.trim()) {
      throw new Error('Message is required');
    }

    const conversation = this.conversationStore.getOrCreateConversation(conversationId);
    const now = new Date();
    const userMessage: ConversationMessage = {
      role: 'User',
      content: message,
      timestamp: now,
    };
    const messages: ConversationMessage[] = [
      {
        role: 'System',
        content: this.options.systemPrompt,
        timestamp: now,
      },
      ...conversation.messages,
      userMessage,
    ];

    const messagesToPersist: ConversationMessage[] = [userMessage];
    const tools = this.toolExecutor.getTools();
    const startedAt = Date.now();
    let totalToolCalls = 0;

    while (true) {
      const limitError = this.getLimitError(messages, startedAt);
      if (limitError) {
        return this.persistAndReturnError(conversation.conversationId, messagesToPersist, limitError);
      }

      const assistantResult = await this.ollamaClient.chat(undefined, messages, tools);
      if (assistantResult.toolCalls.length === 0) {
        const content = this.requiresRepositoryKnowledge(message) && this.retrievalService && this.promptBuilder
          ? await this.answerWithRetrieval(message)
          : assistantResult.content;
        const finalMessage: ConversationMessage = {
          role: 'Assistant',
          content,
          timestamp: new Date(),
        };
        messagesToPersist.push(finalMessage);
        this.conversationStore.appendMessages(conversation.conversationId, messagesToPersist);
        this.logger.log(`Assistant request completed with ${totalToolCalls} tool calls`);

        return {
          conversationId: conversation.conversationId,
          response: content,
        };
      }

      if (assistantResult.content.trim()) {
        const assistantMessage: ConversationMessage = {
          role: 'Assistant',
          content: assistantResult.content,
          timestamp: new Date(),
        };
        messages.push(assistantMessage);
        messagesToPersist.push(assistantMessage);
      }

      for (const toolCall of assistantResult.toolCalls) {
        if (totalToolCalls >= this.options.maximumToolCallsPerRequest) {
          return this.persistAndReturnError(
            conversation.conversationId,
            messagesToPersist,
            `The request could not be completed because it exceeded the maximum tool call limit of ${this.options.maximumToolCallsPerRequest}.`
          );
        }

        const toolStartedAt = Date.now();
        totalToolCalls++;
        this.logger.log(`Assistant requested tool ${toolCall.name} with arguments ${JSON.stringify(toolCall.arguments)}`);

        let toolResult;
        try {
          toolResult = await this.toolExecutor.execute(toolCall.name, toolCall.arguments);
        } catch (error) {
          toolResult = {
            success: false,
            tool: toolCall.name,
            error: error instanceof Error ? error.message : String(error),
          };
        }

        this.logger.log(
          `Assistant tool ${toolResult.tool} finished in ${Date.now() - toolStartedAt}ms with success=${toolResult.success}`
        );

        const toolMessage: ConversationMessage = {
          role: 'Tool',
          content: JSON.stringify(toolResult),
          timestamp: new Date(),
        };
        messages.push(toolMessage);
        messagesToPersist.push(toolMessage);
      }
    }
  }

  private getLimitError(messages: ConversationMessage[], startedAt: number): string | undefined {
    if (this.options.maximumConversationSize > 0 && messages.length > this.options.maximumConversationSize) {
      return `The request could not be completed because the conversation exceeded the maximum size of ${this.options.maximumConversationSize} messages.`;
    }

    if (this.options.maximumExecutionTimeMs > 0 && Date.now() - startedAt > this.options.maximumExecutionTimeMs) {
      return `The request could not be completed because it exceeded the maximum execution time of ${this.options.maximumExecutionTimeMs}ms.`;
    }

    return undefined;
  }

  private persistAndReturnError(
    conversationId: string,
    messagesToPersist: ConversationMessage[],
    error: string
  ): AssistantChatResult {
    messagesToPersist.push({
      role: 'Assistant',
      content: error,
      timestamp: new Date(),
    });
    this.conversationStore.appendMessages(conversationId, messagesToPersist);
    this.logger.warn(error);

    return {
      conversationId,
      response: error,
    };
  }

  private requiresRepositoryKnowledge(message: string): boolean {
    const normalized = message.toLowerCase();
    return [
      'recipe', 'recipes', 'meal', 'meals', 'cook', 'cooking', 'dinner', 'lunch', 'freezer',
      'ingredient', 'ingredients', 'prep', 'preparation', 'what can i', 'what should i', 'recommend',
    ].some((term) => normalized.includes(term));
  }

  private async answerWithRetrieval(question: string): Promise<string> {
    const retrieval = await this.retrievalService.retrieve(question);
    if (retrieval.recipes.length === 0) {
      return 'The repository does not contain enough information to answer that question.\n\nSources: none';
    }

    const prompt = this.promptBuilder.build(question, retrieval.recipes);
    const response = await this.ollamaClient.chat(undefined, [
      { role: 'System', content: this.options.systemPrompt, timestamp: new Date() },
      { role: 'User', content: prompt, timestamp: new Date() },
    ], []);
    const content = response.content?.trim() || 'The repository does not contain enough information to answer that question.';
    return `${content}\n\n${this.formatSources(retrieval.sources)}`;
  }

  private formatSources(sources: SourceAttribution[]): string {
    if (sources.length === 0) {
      return 'Sources: none';
    }

    return ['Sources:', ...sources.map((source) => `- ${source.recipeId}: ${source.title} (similarityScore: ${source.similarityScore.toFixed(6)})`)].join('\n');
  }
}