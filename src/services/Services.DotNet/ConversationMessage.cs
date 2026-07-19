namespace Services.DotNet;

public sealed record ConversationMessage(
    ConversationRole Role,
    string Content,
    DateTimeOffset Timestamp);