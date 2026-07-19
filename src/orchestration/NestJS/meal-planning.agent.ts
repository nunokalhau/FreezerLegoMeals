import { Injectable, Logger } from '@nestjs/common';
import { PromptBuilder } from '../../ai/RAG/NestJS/prompt-builder';
import { RetrievalService, SourceAttribution } from '../../ai/RAG/NestJS/retrieval.service';
import { ConversationMessage } from '../../services/Services.NestJS/conversation-store';
import { OllamaClient } from '../../services/Services.NestJS/ollama.client';
import { ToolExecutor } from '../../services/Services.NestJS/tool-executor';
import { Agent } from './agent.interface';
import { OrchestratorContext } from './orchestrator-context';
import { OrchestratorResult, RetrievedRecipeInfo } from './orchestrator-result';

@Injectable()
export class MealPlanningAgent implements Agent {
  readonly name = 'MealPlanningAgent';
  private readonly logger = new Logger(MealPlanningAgent.name);

  constructor(
    private readonly ollamaClient: OllamaClient,
    private readonly toolExecutor: ToolExecutor,
    private readonly retrievalService?: RetrievalService,
    private readonly promptBuilder?: PromptBuilder
  ) {}

  canHandle(_context: OrchestratorContext): boolean {
    return true;
  }

  async execute(context: OrchestratorContext): Promise<OrchestratorResult> {
    const startedAt = Date.now();
    const messages = [...context.messages];
    const messagesToPersist = [...context.messagesToPersist];
    const tools = this.toolExecutor.getTools();
    const executedTools: string[] = [];
    const retrievedRecipes: RetrievedRecipeInfo[] = [];
    const errors: string[] = [];
    const executionSteps = ['Assistant', 'Orchestrator', this.name];
    let totalToolCalls = 0;

    while (true) {
      const limitError = this.getLimitError(context, messages, startedAt);
      if (limitError) {
        errors.push(limitError);
        return this.buildResult(context, limitError, messagesToPersist, executedTools, retrievedRecipes, executionSteps, errors, startedAt);
      }

      executionSteps.push('Ollama');
      this.logger.log(`${this.name} invoking Ollama for correlation ${context.correlationId}`);
      const assistantResult = await this.ollamaClient.chat(undefined, messages, tools);
      if (assistantResult.toolCalls.length === 0) {
        let content = assistantResult.content;
        if (this.requiresRepositoryKnowledge(context.userRequest) && this.retrievalService && this.promptBuilder) {
          executionSteps.push('Semantic Search', 'Retrieval', 'Prompt Builder', 'RAG');
          const ragResult = await this.answerWithRetrieval(context);
          content = ragResult.response;
          retrievedRecipes.push(...ragResult.retrievedRecipes);
        }

        executionSteps.push('Answer');
        messagesToPersist.push({ role: 'Assistant', content, timestamp: new Date() });
        this.logger.log(`${this.name} completed with ${totalToolCalls} tool calls`);
        return this.buildResult(context, content, messagesToPersist, executedTools, retrievedRecipes, executionSteps, errors, startedAt);
      }

      if (assistantResult.content.trim()) {
        const assistantMessage: ConversationMessage = { role: 'Assistant', content: assistantResult.content, timestamp: new Date() };
        messages.push(assistantMessage);
        messagesToPersist.push(assistantMessage);
      }

      for (const toolCall of assistantResult.toolCalls) {
        if (totalToolCalls >= context.assistantOptions.maximumToolCallsPerRequest) {
          const error = `The request could not be completed because it exceeded the maximum tool call limit of ${context.assistantOptions.maximumToolCallsPerRequest}.`;
          errors.push(error);
          return this.buildResult(context, error, messagesToPersist, executedTools, retrievedRecipes, executionSteps, errors, startedAt);
        }

        totalToolCalls++;
        executedTools.push(toolCall.name);
        executionSteps.push('ToolExecutor');
        const toolStartedAt = Date.now();
        this.logger.log(`${this.name} requested tool ${toolCall.name} with arguments ${JSON.stringify(toolCall.arguments)}`);

        let toolResult;
        try {
          toolResult = await this.toolExecutor.execute(toolCall.name, toolCall.arguments);
        } catch (error) {
          const message = error instanceof Error ? error.message : String(error);
          errors.push(message);
          toolResult = { success: false, tool: toolCall.name, error: message };
        }

        this.logger.log(`${this.name} tool ${toolResult.tool} finished in ${Date.now() - toolStartedAt}ms with success=${toolResult.success}`);
        const toolMessage: ConversationMessage = { role: 'Tool', content: JSON.stringify(toolResult), timestamp: new Date() };
        messages.push(toolMessage);
        messagesToPersist.push(toolMessage);
      }
    }
  }

  private getLimitError(context: OrchestratorContext, messages: ConversationMessage[], startedAt: number): string | undefined {
    if (context.assistantOptions.maximumConversationSize > 0 && messages.length > context.assistantOptions.maximumConversationSize) {
      return `The request could not be completed because the conversation exceeded the maximum size of ${context.assistantOptions.maximumConversationSize} messages.`;
    }

    if (context.assistantOptions.maximumExecutionTimeMs > 0 && Date.now() - startedAt > context.assistantOptions.maximumExecutionTimeMs) {
      return `The request could not be completed because it exceeded the maximum execution time of ${context.assistantOptions.maximumExecutionTimeMs}ms.`;
    }

    return undefined;
  }

  private requiresRepositoryKnowledge(message: string): boolean {
    const normalized = message.toLowerCase();
    return [
      'recipe', 'recipes', 'meal', 'meals', 'cook', 'cooking', 'dinner', 'lunch', 'freezer',
      'ingredient', 'ingredients', 'prep', 'preparation', 'what can i', 'what should i', 'recommend',
    ].some((term) => normalized.includes(term));
  }

  private async answerWithRetrieval(context: OrchestratorContext): Promise<{ response: string; retrievedRecipes: RetrievedRecipeInfo[] }> {
    const retrieval = await this.retrievalService.retrieve(context.userRequest);
    const retrievedRecipes = retrieval.sources.map((source) => ({
      recipeId: source.recipeId,
      title: source.title,
      similarityScore: source.similarityScore,
    }));
    if (retrieval.recipes.length === 0) {
      return { response: 'The repository does not contain enough information to answer that question.\n\nSources: none', retrievedRecipes };
    }

    const prompt = this.promptBuilder.build(context.userRequest, retrieval.recipes);
    const response = await this.ollamaClient.chat(undefined, [
      { role: 'System', content: context.assistantOptions.systemPrompt, timestamp: new Date() },
      { role: 'User', content: prompt, timestamp: new Date() },
    ], []);
    const content = response.content?.trim() || 'The repository does not contain enough information to answer that question.';
    return { response: `${content}\n\n${this.formatSources(retrieval.sources)}`, retrievedRecipes };
  }

  private formatSources(sources: SourceAttribution[]): string {
    if (sources.length === 0) {
      return 'Sources: none';
    }

    return ['Sources:', ...sources.map((source) => `- ${source.recipeId}: ${source.title} (similarityScore: ${source.similarityScore.toFixed(6)})`)].join('\n');
  }

  private buildResult(
    context: OrchestratorContext,
    response: string,
    messagesToPersist: ConversationMessage[],
    executedTools: string[],
    retrievedRecipes: RetrievedRecipeInfo[],
    executionSteps: string[],
    errors: string[],
    startedAt: number
  ): OrchestratorResult {
    this.logger.log(`Orchestration path for ${context.correlationId}: ${executionSteps.join(' -> ')}`);
    return {
      finalResponse: response,
      selectedAgent: this.name,
      executedTools,
      retrievedRecipes,
      executionSteps,
      executionDurationMs: Date.now() - startedAt,
      errors,
      messagesToPersist,
    };
  }
}