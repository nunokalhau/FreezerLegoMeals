namespace Services.DotNet;

public interface IToolExecutor
{
    IReadOnlyList<ToolDefinition> GetTools();

    Task<ToolExecutionResult> ExecuteAsync(
        string toolName,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default);
}