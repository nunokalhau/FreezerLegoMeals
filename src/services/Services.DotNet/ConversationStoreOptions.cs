namespace Services.DotNet;

public sealed class ConversationStoreOptions
{
    public int MaximumConversations { get; set; } = 1000;

    public int MaximumMessagesPerConversation { get; set; } = 50;

    public bool AutomaticTrimming { get; set; } = true;

    public TimeSpan ExpirationTimeout { get; set; } = TimeSpan.FromHours(1);
}