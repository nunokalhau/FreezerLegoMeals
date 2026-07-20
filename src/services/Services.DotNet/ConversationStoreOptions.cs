namespace Services.DotNet;

public sealed class ConversationStoreOptions
{
    public string RedisConnectionString { get; set; } = "localhost:6379,abortConnect=false,connectTimeout=5000,syncTimeout=5000";

    public int MaximumConversations { get; set; } = 1000;

    public int MaximumMessagesPerConversation { get; set; } = 50;

    public bool AutomaticTrimming { get; set; } = true;

    public TimeSpan ExpirationTimeout { get; set; } = TimeSpan.FromHours(1);
}