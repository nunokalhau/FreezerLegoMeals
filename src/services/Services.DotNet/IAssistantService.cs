namespace Services.DotNet;

public interface IAssistantService
{
    Task<AssistantChatResult> ChatAsync(
        string message,
        string? conversationId = null,
        AssistantLocalizationRequest? localization = null,
        CancellationToken cancellationToken = default);
}