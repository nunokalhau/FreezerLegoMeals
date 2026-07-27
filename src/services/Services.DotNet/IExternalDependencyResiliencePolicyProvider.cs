using Polly;

namespace Services.DotNet;

public interface IExternalDependencyResiliencePolicyProvider
{
    IAsyncPolicy<HttpResponseMessage> GetHttpPolicy(ExternalDependency dependency);

    ISyncPolicy GetSyncPolicy(ExternalDependency dependency);

    IAsyncPolicy GetAsyncPolicy(ExternalDependency dependency);

    T Execute<T>(ExternalDependency dependency, Func<T> action);

    void Execute(ExternalDependency dependency, Action action);

    Task<T> ExecuteAsync<T>(ExternalDependency dependency, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default);

    Task ExecuteAsync(ExternalDependency dependency, Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);
}
