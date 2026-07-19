namespace Services.DotNet;

public sealed class ToolExecutor : IToolExecutor
{
    private readonly IToolRegistry _toolRegistry;
    private readonly IReadOnlyDictionary<string, IToolHandler> _handlers;

    public ToolExecutor(IToolRegistry toolRegistry, IEnumerable<IToolHandler> handlers)
    {
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _handlers = (handlers ?? throw new ArgumentNullException(nameof(handlers)))
            .ToDictionary(handler => handler.ToolName, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<ToolDefinition> GetTools()
    {
        return _toolRegistry.GetTools();
    }

    public async Task<ToolExecutionResult> ExecuteAsync(
        string toolName,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var tool = _toolRegistry.FindTool(toolName);

        if (!_handlers.TryGetValue(tool.Name, out var handler))
        {
            return new ToolExecutionResult
            {
                Success = false,
                Tool = tool.Name,
                Error = $"No application service handler registered for tool: {tool.Name}"
            };
        }

        var output = await handler.ExecuteAsync(parameters ?? new Dictionary<string, object?>(), cancellationToken);

        return new ToolExecutionResult
        {
            Success = true,
            Tool = tool.Name,
            Output = output
        };
    }
}