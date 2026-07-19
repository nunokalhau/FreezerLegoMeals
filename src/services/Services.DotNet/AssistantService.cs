namespace Services.DotNet;

public class AssistantService : IAssistantService
{
    private readonly IOllamaClient _ollamaClient;

    public AssistantService(IOllamaClient ollamaClient)
    {
        _ollamaClient = ollamaClient ?? throw new ArgumentNullException(nameof(ollamaClient));
    }

    public async Task<string> ChatAsync(string message, CancellationToken cancellationToken = default)
    {
        return await _ollamaClient.ChatAsync(null, message, cancellationToken);
    }
}