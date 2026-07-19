import { AssistantService } from '../../../services/Services.NestJS/assistant.service';
import { OllamaClient } from '../../../services/Services.NestJS/ollama.client';

describe('AssistantService', () => {
  it('delegates chat requests to OllamaClient', async () => {
    const ollamaClient = {
      chat: jest.fn().mockResolvedValue('assistant response'),
    } as unknown as jest.Mocked<OllamaClient>;
    const service = new AssistantService(ollamaClient);

    const result = await service.chat('Hello');

    expect(result).toBe('assistant response');
    expect(ollamaClient.chat).toHaveBeenCalledWith(undefined, 'Hello');
  });
});