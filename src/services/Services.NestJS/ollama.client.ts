import { Inject, Injectable } from '@nestjs/common';
import { OllamaClientInterface } from './ollama-client.interface';
import { OLLAMA_OPTIONS, OllamaOptions } from './ollama-options';

@Injectable()
export class OllamaClient implements OllamaClientInterface {
  constructor(
    @Inject(OLLAMA_OPTIONS)
    private readonly options: OllamaOptions
  ) {}

  async chat(model: string | undefined, userMessage: string): Promise<string> {
    if (!userMessage || !userMessage.trim()) {
      throw new Error('User message is required');
    }

    const selectedModel = model && model.trim() ? model : this.options.defaultModel;
    if (!selectedModel || !selectedModel.trim()) {
      throw new Error('An Ollama model must be provided or configured as the default model.');
    }

    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), this.options.timeoutMs);

    try {
      const response = await fetch(new URL('/api/chat', this.options.baseUrl), {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          model: selectedModel,
          messages: [
            {
              role: 'user',
              content: userMessage,
            },
          ],
          stream: false,
        }),
        signal: controller.signal,
      });

      if (!response.ok) {
        throw new Error(`Ollama chat request failed with status ${response.status}`);
      }

      const data = await response.json();
      return data?.message?.content || '';
    } finally {
      clearTimeout(timeout);
    }
  }
}