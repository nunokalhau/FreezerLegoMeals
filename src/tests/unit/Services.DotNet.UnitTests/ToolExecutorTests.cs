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
    public async Task ExecuteAsync_ExecutesRegistryWrapperAndParsesJson()
    {
        using var tempRoot = CreateToolRoot();
        var registry = new ToolRegistry(tempRoot.RegistryPath);
        var executor = new PythonToolExecutor(registry, tempRoot.ToolsRoot);
        var parameters = new Dictionary<string, object?> { ["message"] = "hello" };

        var result = await executor.ExecuteAsync("example_alias", parameters);

        Assert.True(result.Success);
        Assert.Equal("example_tool", result.Tool);
        Assert.NotNull(result.Output);
        Assert.Contains("hello", result.Output.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsFailureWhenWrapperIsMissing()
    {
        using var tempFile = CreateRegistryFile();
        var registry = new ToolRegistry(tempFile.Path);
        var executor = new PythonToolExecutor(registry, Path.GetDirectoryName(tempFile.Path)!);

        var result = await executor.ExecuteAsync("example_tool");

        Assert.False(result.Success);
        Assert.Equal("example_tool", result.Tool);
        Assert.Contains("Tool wrapper not found", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnknownTool_ThrowsArgumentException()
    {
        using var tempFile = CreateRegistryFile();
        var registry = new ToolRegistry(tempFile.Path);
                var executor = new PythonToolExecutor(registry, Path.GetDirectoryName(tempFile.Path)!);

        await Assert.ThrowsAsync<ArgumentException>(() => executor.ExecuteAsync("missing_tool"));
    }

        private static TemporaryToolRoot CreateToolRoot()
        {
                var root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                Directory.CreateDirectory(root);
                var registryPath = Path.Combine(root, "tool_registry.json");
                var wrapperPath = Path.Combine(root, "example_tool.py");
                File.WriteAllText(
                        wrapperPath,
                        "import json, sys\nparameters = json.loads(sys.stdin.read() or '{}')\nprint(json.dumps({'handled': True, 'parameters': parameters}))\n");
                File.WriteAllText(
                        registryPath,
                        """
                        {
                            "tools": [
                                {
                                    "name": "example_tool",
                                    "description": "Example tool",
                                    "wrapper": "example_tool.py",
                                    "aliases": ["example_alias"]
                                }
                            ]
                        }
                        """);

                return new TemporaryToolRoot(root, registryPath);
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

    private sealed class TemporaryToolRoot : IDisposable
    {
        public TemporaryToolRoot(string toolsRoot, string registryPath)
        {
            ToolsRoot = toolsRoot;
            RegistryPath = registryPath;
        }

        public string ToolsRoot { get; }

        public string RegistryPath { get; }

        public void Dispose()
        {
            Directory.Delete(ToolsRoot, recursive: true);
        }
    }
}