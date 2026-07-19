namespace Services.DotNet;

public interface IToolHandler
{
    string ToolName { get; }

    Task<object?> ExecuteAsync(IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken);
}