using Xunit;

namespace Services.DotNet.UnitTests;

public class ToolExecutorTests
{
    [Fact]
    public void ToolRegistry_LoadsToolsAndFindsAliases()
    {
        using var tempFile = CreateRegistryFile();
        var registry = new ToolRegistry(tempFile.Path);

        var tools = registry.GetTools();
        var tool = registry.FindTool("example_alias");

        Assert.Single(tools);
        Assert.Equal("example_tool", tool.Name);
    }

    [Fact]
    public async Task ExecuteAsync_DelegatesToRegisteredApplicationHandler()
    {
        using var tempFile = CreateRegistryFile();
        var registry = new ToolRegistry(tempFile.Path);
        var handler = new FakeToolHandler("example_tool", new { Handled = true });
        var executor = new ToolExecutor(registry, new[] { handler });
        var parameters = new Dictionary<string, object?> { ["message"] = "hello" };

        var result = await executor.ExecuteAsync("example_alias", parameters);

        Assert.True(result.Success);
        Assert.Equal("example_tool", result.Tool);
        Assert.Same(handler.Output, result.Output);
        Assert.Same(parameters, handler.Parameters);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotFallBackToCliScripts()
    {
        using var tempFile = CreateRegistryFile();
        var registry = new ToolRegistry(tempFile.Path);
        var executor = new ToolExecutor(registry, Array.Empty<IToolHandler>());

        var result = await executor.ExecuteAsync("example_tool");

        Assert.False(result.Success);
        Assert.Equal("example_tool", result.Tool);
        Assert.Contains("No application service handler", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnknownTool_ThrowsArgumentException()
    {
        using var tempFile = CreateRegistryFile();
        var registry = new ToolRegistry(tempFile.Path);
        var executor = new ToolExecutor(registry, Array.Empty<IToolHandler>());

        await Assert.ThrowsAsync<ArgumentException>(() => executor.ExecuteAsync("missing_tool"));
    }

    private static TemporaryRegistryFile CreateRegistryFile()
    {
        var path = System.IO.Path.GetTempFileName();
        File.WriteAllText(
            path,
            """
            {
              "tools": [
                {
                  "name": "example_tool",
                  "description": "Example tool",
                  "script": "example_tool.py",
                  "aliases": ["example_alias"]
                }
              ]
            }
            """);

        return new TemporaryRegistryFile(path);
    }

    private sealed class TemporaryRegistryFile : IDisposable
    {
        public TemporaryRegistryFile(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public void Dispose()
        {
            File.Delete(Path);
        }
    }

    private sealed class FakeToolHandler : IToolHandler
    {
        public FakeToolHandler(string toolName, object output)
        {
            ToolName = toolName;
            Output = output;
        }

        public string ToolName { get; }

        public object Output { get; }

        public IReadOnlyDictionary<string, object?>? Parameters { get; private set; }

        public Task<object?> ExecuteAsync(IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken)
        {
            Parameters = parameters;
            return Task.FromResult<object?>(Output);
        }
    }
}