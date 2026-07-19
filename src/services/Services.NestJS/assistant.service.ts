import { Inject, Injectable, Optional } from '@nestjs/common';
import { AssistantChatResult, AssistantServiceInterface } from './assistant.service.interface';
import { ASSISTANT_OPTIONS, AssistantOptions, createAssistantOptions } from './assistant-options';
import { ConversationMessage, ConversationStore } from './conversation-store';
import { OllamaClient } from './ollama.client';

@Injectable()
export class AssistantService implements AssistantServiceInterface {
  constructor(
    private readonly ollamaClient: OllamaClient,
    @Inject(ConversationStore)
    private readonly conversationStore: ConversationStore,
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

    const response = await this.ollamaClient.chat(undefined, messages);
    this.conversationStore.appendMessages(conversation.conversationId, [
      userMessage,
      {
        role: 'Assistant',
        content: response,
        timestamp: new Date(),
      },
    ]);

    return {
      conversationId: conversation.conversationId,
      response,
    };
  }
}