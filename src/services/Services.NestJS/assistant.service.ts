import { Injectable } from '@nestjs/common';
import { AssistantServiceInterface } from './assistant.service.interface';
import { OllamaClient } from './ollama.client';

@Injectable()
export class AssistantService implements AssistantServiceInterface {
  constructor(private readonly ollamaClient: OllamaClient) {}

  async chat(message: string): Promise<string> {
    return await this.ollamaClient.chat(undefined, message);
  }
}