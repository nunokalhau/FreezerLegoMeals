namespace Services.DotNet;

public sealed class ToolExecutionResult
{
    public required bool Success { get; init; }

    public required string Tool { get; init; }

    public object? Output { get; init; }

    public string? Error { get; init; }
}