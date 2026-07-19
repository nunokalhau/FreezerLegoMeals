namespace Services.DotNet;

public sealed record ConversationHistory(
    string ConversationId,
    IReadOnlyList<ConversationMessage> Messages);