using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Services.DotNet;

public sealed class PythonToolExecutor : IToolExecutor
{
    // TODO: Add Redis-backed execution metadata/history and reusable result caching when tool execution needs cross-instance observability.
    // TODO: Add RedisToolExecutor, MCPToolExecutor, DockerToolExecutor, and RemoteToolExecutor implementations behind IToolExecutor.
    private readonly IToolRegistry _toolRegistry;
    private readonly string _toolsRoot;
    private readonly string _pythonExecutable;

    public PythonToolExecutor(IToolRegistry toolRegistry, string toolsRoot, string pythonExecutable = "python")
    {
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _toolsRoot = string.IsNullOrWhiteSpace(toolsRoot) ? throw new ArgumentException("Tools root is required", nameof(toolsRoot)) : toolsRoot;
        _pythonExecutable = string.IsNullOrWhiteSpace(pythonExecutable) ? "python" : pythonExecutable;
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
        string wrapper;
        try
        {
            wrapper = ResolveWrapper(tool);
        }
        catch (Exception exception)
        {
            return new ToolExecutionResult
            {
                Success = false,
                Tool = tool.Name,
                Error = exception.Message
            };
        }

        var payload = JsonSerializer.Serialize(parameters ?? new Dictionary<string, object?>());

        var startInfo = new ProcessStartInfo
        {
            FileName = _pythonExecutable,
            Arguments = $"\"{wrapper}\"",
            WorkingDirectory = _toolsRoot,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        await process.StandardInput.WriteAsync(payload.AsMemory(), cancellationToken);
        process.StandardInput.Close();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            return new ToolExecutionResult
            {
                Success = false,
                Tool = tool.Name,
                Error = string.IsNullOrWhiteSpace(stderr) ? $"Tool wrapper exited with code {process.ExitCode}" : stderr.Trim()
            };
        }

        try
        {
            return new ToolExecutionResult
            {
                Success = true,
                Tool = tool.Name,
                Output = JsonSerializer.Deserialize<object>(stdout)
            };
        }
        catch (JsonException exception)
        {
            return new ToolExecutionResult
            {
                Success = false,
                Tool = tool.Name,
                Error = $"Tool wrapper returned invalid JSON: {exception.Message}"
            };
        }
    }

    private string ResolveWrapper(ToolDefinition tool)
    {
        var wrapper = string.IsNullOrWhiteSpace(tool.Wrapper) ? tool.Script : tool.Wrapper;
        if (string.IsNullOrWhiteSpace(wrapper))
        {
            throw new InvalidOperationException($"Tool '{tool.Name}' does not define a wrapper.");
        }

        var path = Path.GetFullPath(Path.Combine(_toolsRoot, wrapper));
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Tool wrapper not found for '{tool.Name}'.", path);
        }

        return path;
    }
}
