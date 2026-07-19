namespace WebApi.DotNet.Contracts.Responses;

public class AssistantChatResponse
{
    public string ConversationId { get; set; } = string.Empty;

    public string Response { get; set; } = string.Empty;
}