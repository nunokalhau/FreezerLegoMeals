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

    const result = await client.chat(undefined, [
      { role: 'System', content: 'system prompt', timestamp: new Date() },
      { role: 'User', content: 'Hello', timestamp: new Date() },
    ], [{ name: 'example_tool', description: 'Example tool', parameters: ['--message'] }]);

    expect(result).toEqual({ content: 'Hello from Ollama', toolCalls: [] });
    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [url, init] = fetchMock.mock.calls[0];
    expect((url as URL).toString()).toBe('http://localhost:11434/api/chat');
    expect(init?.method).toBe('POST');
    expect(JSON.parse(init?.body as string)).toEqual({
      model: 'llama3.2',
      messages: [
        {
          role: 'system',
          content: 'system prompt',
        },
        {
          role: 'user',
          content: 'Hello',
        },
      ],
      tools: [
        {
          type: 'function',
          function: {
            name: 'example_tool',
            description: 'Example tool',
            parameters: {
              type: 'object',
              properties: {
                message: { type: 'string', description: 'Parameter for example_tool' },
              },
              required: [],
            },
          },
        },
      ],
      stream: false,
    });
  });

  it('includes output description and result example in tool descriptions', async () => {
    const fetchMock = jest.spyOn(global, 'fetch').mockResolvedValue({
      ok: true,
      json: async () => ({ message: { content: 'ok' } }),
    } as Response);
    const client = new OllamaClient({
      baseUrl: 'http://localhost:11434',
      defaultModel: 'llama3.2',
      timeoutMs: 30000,
    });

    await client.chat(undefined, [{ role: 'User', content: 'Hello', timestamp: new Date() }], [{
      name: 'search_recipes',
      description: 'Primary recipe discovery tool.',
      output_description: 'Recipe discovery cards.',
      result_example: { id: 'chicken-teriyaki', name: 'Chicken Teriyaki' },
    }]);

    const [, init] = fetchMock.mock.calls[0];
    const description = JSON.parse(init?.body as string).tools[0].function.description;
    expect(description).toContain('Recipe discovery cards');
    expect(description).toContain('chicken-teriyaki');
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

    await client.chat('custom-model', [{ role: 'User', content: 'Hello', timestamp: new Date() }], []);

    const [, init] = fetchMock.mock.calls[0];
    expect(JSON.parse(init?.body as string).model).toBe('custom-model');
  });

  it('rejects empty user messages', async () => {
    const client = new OllamaClient({
      baseUrl: 'http://localhost:11434',
      defaultModel: 'llama3.2',
      timeoutMs: 30000,
    });

    await expect(client.chat('llama3.2', [])).rejects.toThrow('At least one chat message is required');
  });

  it('returns native tool calls from Ollama responses', async () => {
    jest.spyOn(global, 'fetch').mockResolvedValue({
      ok: true,
      json: async () => ({
        message: {
          content: '',
          tool_calls: [
            {
              function: {
                name: 'example_tool',
                arguments: { message: 'hello' },
              },
            },
          ],
        },
      }),
    } as Response);
    const client = new OllamaClient({
      baseUrl: 'http://localhost:11434',
      defaultModel: 'llama3.2',
      timeoutMs: 30000,
    });

    const result = await client.chat(undefined, [{ role: 'User', content: 'Hello', timestamp: new Date() }], []);

    expect(result.toolCalls).toEqual([{ name: 'example_tool', arguments: { message: 'hello' } }]);
  });

  it('retries without tools when Ollama rejects the tool payload', async () => {
    const fetchMock = jest.spyOn(global, 'fetch')
      .mockResolvedValueOnce({ ok: false, status: 400, json: async () => ({}) } as Response)
      .mockResolvedValueOnce({ ok: true, json: async () => ({ message: { content: 'fallback ok' } }) } as Response);
    const client = new OllamaClient({
      baseUrl: 'http://localhost:11434',
      defaultModel: 'llama3.2',
      timeoutMs: 30000,
    });

    const result = await client.chat(undefined, [{ role: 'User', content: 'Hello', timestamp: new Date() }], [{ name: 'search_recipes', description: 'Search', parameters: ['ingredients'] }]);

    expect(result.content).toBe('fallback ok');
    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(JSON.parse(fetchMock.mock.calls[1][1]?.body as string).tools).toEqual([]);
  });
});