using Services.DotNet;

namespace Orchestration.DotNet;

public sealed record OrchestratorContext(
    string UserRequest,
    DateTimeOffset CurrentTimestamp,
    string CorrelationId,
    IReadOnlyDictionary<string, object?> Metadata,
    string ConversationId,
    IReadOnlyList<ConversationMessage> Messages,
    IReadOnlyList<ConversationMessage> MessagesToPersist,
    AssistantOptions AssistantOptions)
{
    // TODO: Add Conversation Memory references when that phase starts.
    // TODO: Add Redis-backed orchestration state when distributed execution is introduced.
}