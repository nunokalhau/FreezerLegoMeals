import { Inject, Injectable } from '@nestjs/common';
import { OllamaClientInterface } from './ollama-client.interface';
import { ConversationMessage, ConversationRole } from './conversation-store';
import { OllamaChatResult } from './ollama-chat-result';
import { OLLAMA_OPTIONS, OllamaOptions } from './ollama-options';
import { ToolDefinition } from './tool-registry';

@Injectable()
export class OllamaClient implements OllamaClientInterface {
  constructor(
    @Inject(OLLAMA_OPTIONS)
    private readonly options: OllamaOptions
  ) {}

  async chat(model: string | undefined, messages: ConversationMessage[], tools: ToolDefinition[] = []): Promise<OllamaChatResult> {
    if (!Array.isArray(messages) || messages.length === 0) {
      throw new Error('At least one chat message is required');
    }

    const selectedModel = model && model.trim() ? model : this.options.defaultModel;
    if (!selectedModel || !selectedModel.trim()) {
      throw new Error('An Ollama model must be provided or configured as the default model.');
    }

    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), this.options.timeoutMs);

    try {
      let response = await fetch(new URL('/api/chat', this.options.baseUrl), {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          model: selectedModel,
          messages: messages.map((message) => ({
            role: this.toOllamaRole(message.role),
            content: message.content,
          })),
          tools: tools.map((tool) => this.toOllamaTool(tool)),
          stream: false,
        }),
        signal: controller.signal,
      });

      if (!response.ok && response.status === 400 && tools.length > 0) {
        response = await fetch(new URL('/api/chat', this.options.baseUrl), {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({
            model: selectedModel,
            messages: messages.map((message) => ({
              role: this.toOllamaRole(message.role),
              content: message.content,
            })),
            tools: [],
            stream: false,
          }),
          signal: controller.signal,
        });
      }

      if (!response.ok) {
        throw new Error(`Ollama chat request failed with status ${response.status}`);
      }

      const data = await response.json();
      return {
        content: data?.message?.content || '',
        toolCalls: (data?.message?.tool_calls || [])
          .filter((toolCall) => toolCall?.function?.name)
          .map((toolCall) => ({
            name: toolCall.function.name,
            arguments: toolCall.function.arguments || {},
          })),
      };
    } finally {
      clearTimeout(timeout);
    }
  }

  private toOllamaRole(role: ConversationRole): string {
    return role.toLowerCase();
  }

  private toOllamaTool(tool: ToolDefinition) {
    const properties = Object.fromEntries(
      (tool.parameters || []).map((parameter) => [
        parameter.replace(/^-+/, '').replace(/-/g, '_'),
        { type: 'string', description: `Parameter for ${tool.name}` },
      ])
    );

    return {
      type: 'function',
      function: {
        name: tool.name,
        description: this.buildToolDescription(tool),
        parameters: {
          type: 'object',
          properties,
          required: [],
        },
      },
    };
  }

  private buildToolDescription(tool: ToolDefinition): string {
    return [
      tool.description,
      tool.output_description ? `Output: ${tool.output_description}` : undefined,
      tool.result_example ? `Result example: ${JSON.stringify(tool.result_example)}` : undefined,
    ].filter(Boolean).join('\n');
  }
}