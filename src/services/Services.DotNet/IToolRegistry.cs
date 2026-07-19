namespace Services.DotNet;

public interface IToolRegistry
{
    IReadOnlyList<ToolDefinition> GetTools();

    ToolDefinition FindTool(string toolName);
}