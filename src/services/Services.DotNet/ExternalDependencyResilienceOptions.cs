namespace Services.DotNet;

public sealed class DependencyResiliencePolicyOptions
{
    public bool Enabled { get; set; } = true;

    public int RetryCount { get; set; } = 2;

    public int RetryBaseDelayMilliseconds { get; set; } = 200;

    public int TimeoutMilliseconds { get; set; } = 30_000;

    public int ExceptionsAllowedBeforeBreaking { get; set; } = 5;

    public int CircuitBreakerDurationSeconds { get; set; } = 30;
}

public sealed class ExternalDependencyResilienceOptions
{
    public DependencyResiliencePolicyOptions Ollama { get; set; } = new();

    public DependencyResiliencePolicyOptions ChromaDb { get; set; } = new();

    public DependencyResiliencePolicyOptions Redis { get; set; } = new()
    {
        TimeoutMilliseconds = 5_000,
        RetryCount = 1,
        ExceptionsAllowedBeforeBreaking = 3,
        CircuitBreakerDurationSeconds = 15
    };

    public DependencyResiliencePolicyOptions PythonToolExecution { get; set; } = new()
    {
        TimeoutMilliseconds = 90_000,
        RetryCount = 0,
        ExceptionsAllowedBeforeBreaking = 3,
        CircuitBreakerDurationSeconds = 20
    };

    public DependencyResiliencePolicyOptions Get(ExternalDependency dependency)
    {
        return dependency switch
        {
            ExternalDependency.Ollama => Ollama,
            ExternalDependency.ChromaDb => ChromaDb,
            ExternalDependency.Redis => Redis,
            ExternalDependency.PythonToolExecution => PythonToolExecution,
            _ => new DependencyResiliencePolicyOptions { Enabled = false }
        };
    }
}
