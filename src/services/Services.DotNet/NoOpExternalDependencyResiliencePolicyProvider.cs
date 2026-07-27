using Polly;

namespace Services.DotNet;

public sealed class NoOpExternalDependencyResiliencePolicyProvider : IExternalDependencyResiliencePolicyProvider
{
    public static NoOpExternalDependencyResiliencePolicyProvider Instance { get; } = new();

    private static readonly IAsyncPolicy<HttpResponseMessage> NoOpHttpPolicy = Policy.NoOpAsync<HttpResponseMessage>();
    private static readonly ISyncPolicy NoOpSyncPolicy = Policy.NoOp();
    private static readonly IAsyncPolicy NoOpAsyncPolicy = Policy.NoOpAsync();

    private NoOpExternalDependencyResiliencePolicyProvider()
    {
    }

    public IAsyncPolicy<HttpResponseMessage> GetHttpPolicy(ExternalDependency dependency) => NoOpHttpPolicy;

    public ISyncPolicy GetSyncPolicy(ExternalDependency dependency) => NoOpSyncPolicy;

    public IAsyncPolicy GetAsyncPolicy(ExternalDependency dependency) => NoOpAsyncPolicy;

    public T Execute<T>(ExternalDependency dependency, Func<T> action) => action();

    public void Execute(ExternalDependency dependency, Action action) => action();

    public Task<T> ExecuteAsync<T>(ExternalDependency dependency, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default) => action(cancellationToken);

    public Task ExecuteAsync(ExternalDependency dependency, Func<CancellationToken, Task> action, CancellationToken cancellationToken = default) => action(cancellationToken);
}
