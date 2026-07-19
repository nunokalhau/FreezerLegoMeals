namespace Services.DotNet;

public sealed class AssistantOptions
{
    public string SystemPrompt { get; set; } = "You are a helpful meal planning assistant.";

    public int MaximumToolCallsPerRequest { get; set; } = 10;

    public int MaximumConversationSize { get; set; } = 100;

    public TimeSpan MaximumExecutionTime { get; set; } = TimeSpan.FromMinutes(2);
}