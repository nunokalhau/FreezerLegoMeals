using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Services.DotNet;

public sealed class PythonToolExecutor : IToolExecutor
{
    private static readonly ActivitySource ActivitySource = new("FreezerLegoMeals.AI");
    // TODO: Add Redis-backed execution metadata/history and reusable result caching when tool execution needs cross-instance observability.
    // TODO: Add RedisToolExecutor, MCPToolExecutor, DockerToolExecutor, and RemoteToolExecutor implementations behind IToolExecutor.
    private readonly IToolRegistry _toolRegistry;
    private readonly string _toolsRoot;
    private readonly string _pythonExecutable;
    private readonly ILogger<PythonToolExecutor> _logger;

    public PythonToolExecutor(IToolRegistry toolRegistry, string toolsRoot, string pythonExecutable = "python", ILogger<PythonToolExecutor>? logger = null)
    {
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _toolsRoot = string.IsNullOrWhiteSpace(toolsRoot) ? throw new ArgumentException("Tools root is required", nameof(toolsRoot)) : toolsRoot;
        _pythonExecutable = string.IsNullOrWhiteSpace(pythonExecutable) ? "python" : pythonExecutable;
        _logger = logger ?? NullLogger<PythonToolExecutor>.Instance;
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
        using var activity = ActivitySource.StartActivity("tool.execute.python", ActivityKind.Internal);
        activity?.SetTag("tool.name", toolName);
        activity?.SetTag("tool.executor", "python");
        activity?.SetTag("tool.parameter_count", parameters?.Count ?? 0);

        var startedAt = Stopwatch.StartNew();
        var tool = _toolRegistry.FindTool(toolName);
        string wrapper;
        try
        {
            wrapper = ResolveWrapper(tool);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Tool execution failed during wrapper resolution tool={ToolName}",
                tool.Name);
            activity?.SetTag("tool.success", false);
            activity?.SetTag("tool.failure_stage", "resolve-wrapper");
            return new ToolExecutionResult
            {
                Success = false,
                Tool = tool.Name,
                Error = exception.Message
            };
        }

        var payload = JsonSerializer.Serialize(parameters ?? new Dictionary<string, object?>());
        _logger.LogInformation(
            "Tool execution started tool={ToolName} wrapper={Wrapper} parameterCount={ParameterCount}",
            tool.Name,
            wrapper,
            parameters?.Count ?? 0);

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
        startedAt.Stop();

        if (process.ExitCode != 0)
        {
            _logger.LogWarning(
                "Tool execution failed tool={ToolName} exitCode={ExitCode} latencyMs={LatencyMs} stderrLength={StderrLength}",
                tool.Name,
                process.ExitCode,
                startedAt.Elapsed.TotalMilliseconds,
                stderr.Length);
            activity?.SetTag("tool.success", false);
            activity?.SetTag("tool.exit_code", process.ExitCode);
            activity?.SetTag("tool.latency_ms", startedAt.Elapsed.TotalMilliseconds);
            return new ToolExecutionResult
            {
                Success = false,
                Tool = tool.Name,
                Error = string.IsNullOrWhiteSpace(stderr) ? $"Tool wrapper exited with code {process.ExitCode}" : stderr.Trim()
            };
        }

        try
        {
            var output = JsonSerializer.Deserialize<object>(stdout);
            _logger.LogInformation(
                "Tool execution completed tool={ToolName} success={Success} latencyMs={LatencyMs} stdoutLength={StdoutLength}",
                tool.Name,
                true,
                startedAt.Elapsed.TotalMilliseconds,
                stdout.Length);
            activity?.SetTag("tool.success", true);
            activity?.SetTag("tool.exit_code", process.ExitCode);
            activity?.SetTag("tool.latency_ms", startedAt.Elapsed.TotalMilliseconds);
            return new ToolExecutionResult
            {
                Success = true,
                Tool = tool.Name,
                Output = output
            };
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(
                exception,
                "Tool execution returned invalid JSON tool={ToolName} latencyMs={LatencyMs}",
                tool.Name,
                startedAt.Elapsed.TotalMilliseconds);
            activity?.SetTag("tool.success", false);
            activity?.SetTag("tool.failure_stage", "deserialize-output");
            activity?.SetTag("tool.latency_ms", startedAt.Elapsed.TotalMilliseconds);
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
