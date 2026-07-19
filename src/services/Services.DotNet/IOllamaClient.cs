namespace Services.DotNet;

public interface IOllamaClient
{
    Task<OllamaChatResult> ChatAsync(
        string? model,
        IReadOnlyList<ConversationMessage> messages,
        IReadOnlyList<ToolDefinition> tools,
        CancellationToken cancellationToken = default);
}