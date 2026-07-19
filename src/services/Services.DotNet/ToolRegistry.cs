using System.Text.Json;

namespace Services.DotNet;

public sealed class ToolRegistry : IToolRegistry
{
    private readonly IReadOnlyList<ToolDefinition> _tools;

    public ToolRegistry(string registryPath)
    {
        if (string.IsNullOrWhiteSpace(registryPath))
        {
            throw new ArgumentException("Registry path is required", nameof(registryPath));
        }

        _tools = LoadTools(registryPath);
    }

    public IReadOnlyList<ToolDefinition> GetTools()
    {
        return _tools;
    }

    public ToolDefinition FindTool(string toolName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

        var tool = _tools.FirstOrDefault(item =>
            string.Equals(item.Name, toolName, StringComparison.OrdinalIgnoreCase) ||
            item.Aliases.Any(alias => string.Equals(alias, toolName, StringComparison.OrdinalIgnoreCase)));

        return tool ?? throw new ArgumentException($"Unknown tool: {toolName}", nameof(toolName));
    }

    private static IReadOnlyList<ToolDefinition> LoadTools(string registryPath)
    {
        using var stream = File.OpenRead(registryPath);
        var document = JsonSerializer.Deserialize<ToolRegistryDocument>(stream);

        if (document?.Tools is not { Count: > 0 })
        {
            throw new InvalidOperationException("Tool registry must contain a tools array");
        }

        return document.Tools;
    }

    private sealed class ToolRegistryDocument
    {
        [System.Text.Json.Serialization.JsonPropertyName("tools")]
        public List<ToolDefinition> Tools { get; init; } = [];
    }
}