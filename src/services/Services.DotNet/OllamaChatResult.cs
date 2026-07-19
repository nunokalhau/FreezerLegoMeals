namespace Services.DotNet;

public sealed record OllamaChatResult(string Content, IReadOnlyList<AssistantToolCall> ToolCalls)
{
    public bool HasToolCalls => ToolCalls.Count > 0;
}