namespace Services.DotNet;

public interface IOllamaClient
{
    Task<string> ChatAsync(string? model, IReadOnlyList<ConversationMessage> messages, CancellationToken cancellationToken = default);
}