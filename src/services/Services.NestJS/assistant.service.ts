import { Inject, Injectable, Logger, Optional } from '@nestjs/common';
import { AssistantChatResult, AssistantServiceInterface } from './assistant.service.interface';
import { ASSISTANT_OPTIONS, AssistantOptions, createAssistantOptions } from './assistant-options';
import { ConversationMessage, ConversationStore } from './conversation-store';
import { OrchestratorService } from '../../orchestration/NestJS/orchestrator.service';

@Injectable()
export class AssistantService implements AssistantServiceInterface {
  private readonly logger = new Logger(AssistantService.name);

  constructor(
    @Inject(ConversationStore)
    private readonly conversationStore: ConversationStore,
    private readonly orchestrator: OrchestratorService,
    @Optional()
    @Inject(ASSISTANT_OPTIONS)
    private readonly options: AssistantOptions = createAssistantOptions()
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
    const result = await this.orchestrator.execute({
      userRequest: message,
      currentTimestamp: now,
      correlationId: crypto.randomUUID(),
      metadata: {},
      conversationId: conversation.conversationId,
      messages,
      messagesToPersist,
      assistantOptions: this.options,
    });

    this.conversationStore.appendMessages(conversation.conversationId, result.messagesToPersist);
    if (result.errors.length > 0) {
      this.logger.warn(`Assistant request completed with orchestration errors: ${result.errors.join('; ')}`);
    }

    return {
      conversationId: conversation.conversationId,
      response: result.finalResponse,
    };
  }
}