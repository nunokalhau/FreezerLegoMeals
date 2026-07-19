namespace WebApi.DotNet.Contracts.Requests;

public class AssistantChatRequest
{
    public string Message { get; set; } = string.Empty;

    public string? ConversationId { get; set; }
}