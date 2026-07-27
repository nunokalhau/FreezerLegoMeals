using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;
using Polly.Timeout;

namespace Services.DotNet;

public sealed class PollyExternalDependencyResiliencePolicyProvider : IExternalDependencyResiliencePolicyProvider
{
    private readonly IReadOnlyDictionary<ExternalDependency, IAsyncPolicy<HttpResponseMessage>> _httpPolicies;
    private readonly IReadOnlyDictionary<ExternalDependency, ISyncPolicy> _syncPolicies;
    private readonly IReadOnlyDictionary<ExternalDependency, IAsyncPolicy> _asyncPolicies;
    private readonly ILogger<PollyExternalDependencyResiliencePolicyProvider> _logger;

    public PollyExternalDependencyResiliencePolicyProvider(
        IOptions<ExternalDependencyResilienceOptions> options,
        ILogger<PollyExternalDependencyResiliencePolicyProvider>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        _logger = logger ?? NullLogger<PollyExternalDependencyResiliencePolicyProvider>.Instance;
        var configuredOptions = options.Value ?? new ExternalDependencyResilienceOptions();

        _httpPolicies = new Dictionary<ExternalDependency, IAsyncPolicy<HttpResponseMessage>>
        {
            [ExternalDependency.Ollama] = BuildHttpPolicy(ExternalDependency.Ollama, configuredOptions.Get(ExternalDependency.Ollama)),
            [ExternalDependency.ChromaDb] = BuildHttpPolicy(ExternalDependency.ChromaDb, configuredOptions.Get(ExternalDependency.ChromaDb))
        };

        _syncPolicies = new Dictionary<ExternalDependency, ISyncPolicy>
        {
            [ExternalDependency.Redis] = BuildSyncPolicy(ExternalDependency.Redis, configuredOptions.Get(ExternalDependency.Redis))
        };

        _asyncPolicies = new Dictionary<ExternalDependency, IAsyncPolicy>
        {
            [ExternalDependency.Redis] = BuildAsyncPolicy(ExternalDependency.Redis, configuredOptions.Get(ExternalDependency.Redis)),
            [ExternalDependency.PythonToolExecution] = BuildAsyncPolicy(ExternalDependency.PythonToolExecution, configuredOptions.Get(ExternalDependency.PythonToolExecution))
        };
    }

    public IAsyncPolicy<HttpResponseMessage> GetHttpPolicy(ExternalDependency dependency)
    {
        return _httpPolicies.TryGetValue(dependency, out var policy)
            ? policy
            : Policy.NoOpAsync<HttpResponseMessage>();
    }

    public ISyncPolicy GetSyncPolicy(ExternalDependency dependency)
    {
        return _syncPolicies.TryGetValue(dependency, out var policy)
            ? policy
            : Policy.NoOp();
    }

    public IAsyncPolicy GetAsyncPolicy(ExternalDependency dependency)
    {
        return _asyncPolicies.TryGetValue(dependency, out var policy)
            ? policy
            : Policy.NoOpAsync();
    }

    public T Execute<T>(ExternalDependency dependency, Func<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return GetSyncPolicy(dependency).Execute(action);
    }

    public void Execute(ExternalDependency dependency, Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        GetSyncPolicy(dependency).Execute(action);
    }

    public Task<T> ExecuteAsync<T>(ExternalDependency dependency, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        return GetAsyncPolicy(dependency).ExecuteAsync((_, ct) => action(ct), new Context(dependency.ToString()), cancellationToken);
    }

    public Task ExecuteAsync(ExternalDependency dependency, Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        return GetAsyncPolicy(dependency).ExecuteAsync((_, ct) => action(ct), new Context(dependency.ToString()), cancellationToken);
    }

    private IAsyncPolicy<HttpResponseMessage> BuildHttpPolicy(ExternalDependency dependency, DependencyResiliencePolicyOptions options)
    {
        if (!options.Enabled)
            return Policy.NoOpAsync<HttpResponseMessage>();

        var httpBuilder = HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(response => (int)response.StatusCode == 429)
            .Or<TimeoutRejectedException>();

        var timeout = Policy.TimeoutAsync<HttpResponseMessage>(GetTimeout(options));

        var breaker = Policy
            .Handle<Exception>(ShouldHandleException)
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: Math.Max(1, options.ExceptionsAllowedBeforeBreaking),
                durationOfBreak: GetBreakDuration(options),
                onBreak: (exception, breakDelay) => _logger.LogWarning(
                    exception,
                    "Resilience circuit opened dependency={Dependency} breakDurationMs={BreakDurationMs}",
                    dependency,
                    breakDelay.TotalMilliseconds),
                onReset: () => _logger.LogInformation("Resilience circuit reset dependency={Dependency}", dependency),
                onHalfOpen: () => _logger.LogInformation("Resilience circuit half-open dependency={Dependency}", dependency));

        var retry = httpBuilder
            .WaitAndRetryAsync(
                retryCount: Math.Max(0, options.RetryCount),
                sleepDurationProvider: attempt => GetRetryDelay(options, attempt),
                onRetry: (outcome, delay, retryAttempt, _) =>
                {
                    _logger.LogWarning(
                        "Resilience retry dependency={Dependency} attempt={RetryAttempt} delayMs={DelayMs} outcome={Outcome}",
                        dependency,
                        retryAttempt,
                        delay.TotalMilliseconds,
                        DescribeHttpOutcome(outcome));
                });

        return Policy.WrapAsync(retry, breaker.AsAsyncPolicy<HttpResponseMessage>(), timeout);
    }

    private IAsyncPolicy BuildAsyncPolicy(ExternalDependency dependency, DependencyResiliencePolicyOptions options)
    {
        if (!options.Enabled)
            return Policy.NoOpAsync();

        var timeout = Policy.TimeoutAsync(GetTimeout(options));

        var breaker = Policy
            .Handle<Exception>(ShouldHandleException)
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: Math.Max(1, options.ExceptionsAllowedBeforeBreaking),
                durationOfBreak: GetBreakDuration(options),
                onBreak: (exception, breakDelay) => _logger.LogWarning(
                    exception,
                    "Resilience circuit opened dependency={Dependency} breakDurationMs={BreakDurationMs}",
                    dependency,
                    breakDelay.TotalMilliseconds),
                onReset: () => _logger.LogInformation("Resilience circuit reset dependency={Dependency}", dependency),
                onHalfOpen: () => _logger.LogInformation("Resilience circuit half-open dependency={Dependency}", dependency));

        var retry = Policy
            .Handle<Exception>(ShouldHandleException)
            .WaitAndRetryAsync(
                retryCount: Math.Max(0, options.RetryCount),
                sleepDurationProvider: attempt => GetRetryDelay(options, attempt),
                onRetry: (exception, delay, retryAttempt, _) =>
                {
                    _logger.LogWarning(
                        exception,
                        "Resilience retry dependency={Dependency} attempt={RetryAttempt} delayMs={DelayMs}",
                        dependency,
                        retryAttempt,
                        delay.TotalMilliseconds);
                });

        return Policy.WrapAsync(retry, breaker, timeout);
    }

    private ISyncPolicy BuildSyncPolicy(ExternalDependency dependency, DependencyResiliencePolicyOptions options)
    {
        if (!options.Enabled)
            return Policy.NoOp();

        var timeout = Policy.Timeout(GetTimeout(options));

        var breaker = Policy
            .Handle<Exception>(ShouldHandleException)
            .CircuitBreaker(
                exceptionsAllowedBeforeBreaking: Math.Max(1, options.ExceptionsAllowedBeforeBreaking),
                durationOfBreak: GetBreakDuration(options),
                onBreak: (exception, breakDelay) => _logger.LogWarning(
                    exception,
                    "Resilience circuit opened dependency={Dependency} breakDurationMs={BreakDurationMs}",
                    dependency,
                    breakDelay.TotalMilliseconds),
                onReset: () => _logger.LogInformation("Resilience circuit reset dependency={Dependency}", dependency),
                onHalfOpen: () => _logger.LogInformation("Resilience circuit half-open dependency={Dependency}", dependency));

        var retry = Policy
            .Handle<Exception>(ShouldHandleException)
            .WaitAndRetry(
                retryCount: Math.Max(0, options.RetryCount),
                sleepDurationProvider: attempt => GetRetryDelay(options, attempt),
                onRetry: (exception, delay, retryAttempt, _) =>
                {
                    _logger.LogWarning(
                        exception,
                        "Resilience retry dependency={Dependency} attempt={RetryAttempt} delayMs={DelayMs}",
                        dependency,
                        retryAttempt,
                        delay.TotalMilliseconds);
                });

        return Policy.Wrap(retry, breaker, timeout);
    }

    private static bool ShouldHandleException(Exception exception)
    {
        return exception is not OperationCanceledException || exception is TimeoutRejectedException;
    }

    private static TimeSpan GetRetryDelay(DependencyResiliencePolicyOptions options, int retryAttempt)
    {
        var baseDelay = Math.Max(1, options.RetryBaseDelayMilliseconds);
        var scale = Math.Pow(2, Math.Max(0, retryAttempt - 1));
        return TimeSpan.FromMilliseconds(baseDelay * scale);
    }

    private static TimeSpan GetTimeout(DependencyResiliencePolicyOptions options)
    {
        return TimeSpan.FromMilliseconds(Math.Max(1, options.TimeoutMilliseconds));
    }

    private static TimeSpan GetBreakDuration(DependencyResiliencePolicyOptions options)
    {
        return TimeSpan.FromSeconds(Math.Max(1, options.CircuitBreakerDurationSeconds));
    }

    private static string DescribeHttpOutcome(DelegateResult<HttpResponseMessage> outcome)
    {
        if (outcome.Exception is not null)
            return outcome.Exception.GetType().Name;

        return outcome.Result is null
            ? "none"
            : $"HTTP {(int)outcome.Result.StatusCode}";
    }

    private static bool IsTransientHttpResponse(HttpResponseMessage response)
    {
        return (int)response.StatusCode >= 500 ||
               response.StatusCode == System.Net.HttpStatusCode.RequestTimeout ||
               (int)response.StatusCode == 429;
    }

}
