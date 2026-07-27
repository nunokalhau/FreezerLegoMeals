import { AppController } from '../../../api/WebApi.NestJS/app.controller';

describe('AppController assistant chat contract', () => {
  it('keeps the assistant endpoint response shape for RAG-style answers', async () => {
    const assistantService = {
      chat: jest.fn().mockResolvedValue({
        conversationId: 'conversation-1',
        response: 'Use the spicy chicken recipe.\n\nSources:\n- 1: Spicy Chicken (similarityScore: 0.910000)',
      }),
    };

    const controller = new AppController(
      { getHello: () => 'ok' } as any,
      assistantService as any,
      {} as any,
      {} as any
    );

    const response = await controller.chatWithAssistant({ message: 'What spicy chicken meal can I cook?' });

    expect(Object.keys(response).sort()).toEqual(['conversationId', 'response']);
    expect(response.response).toContain('Sources:');
    expect(response.response).toContain('1: Spicy Chicken');
    expect(assistantService.chat).toHaveBeenCalledWith('What spicy chicken meal can I cook?', undefined);
  });
});
