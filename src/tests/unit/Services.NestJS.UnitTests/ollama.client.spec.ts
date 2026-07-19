import { OllamaClient } from '../../../services/Services.NestJS/ollama.client';

describe('OllamaClient', () => {
  afterEach(() => {
    jest.restoreAllMocks();
  });

  it('uses the configured default model and returns assistant content', async () => {
    const fetchMock = jest.spyOn(global, 'fetch').mockResolvedValue({
      ok: true,
      json: async () => ({
        message: {
          role: 'assistant',
          content: 'Hello from Ollama',
        },
      }),
    } as Response);
    const client = new OllamaClient({
      baseUrl: 'http://localhost:11434',
      defaultModel: 'llama3.2',
      timeoutMs: 30000,
    });

    const result = await client.chat(undefined, 'Hello');

    expect(result).toBe('Hello from Ollama');
    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [url, init] = fetchMock.mock.calls[0];
    expect((url as URL).toString()).toBe('http://localhost:11434/api/chat');
    expect(init?.method).toBe('POST');
    expect(JSON.parse(init?.body as string)).toEqual({
      model: 'llama3.2',
      messages: [
        {
          role: 'user',
          content: 'Hello',
        },
      ],
      stream: false,
    });
  });

  it('uses the provided model when present', async () => {
    const fetchMock = jest.spyOn(global, 'fetch').mockResolvedValue({
      ok: true,
      json: async () => ({ message: { content: 'ok' } }),
    } as Response);
    const client = new OllamaClient({
      baseUrl: 'http://localhost:11434',
      defaultModel: 'default-model',
      timeoutMs: 30000,
    });

    await client.chat('custom-model', 'Hello');

    const [, init] = fetchMock.mock.calls[0];
    expect(JSON.parse(init?.body as string).model).toBe('custom-model');
  });

  it('rejects empty user messages', async () => {
    const client = new OllamaClient({
      baseUrl: 'http://localhost:11434',
      defaultModel: 'llama3.2',
      timeoutMs: 30000,
    });

    await expect(client.chat('llama3.2', ' ')).rejects.toThrow('User message is required');
  });
});