using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Polly.Timeout;
using Services.DotNet;
using Xunit;

namespace Services.DotNet.UnitTests;

public class PollyExternalDependencyResiliencePolicyProviderTests
{
    [Fact]
    public async Task ExecuteAsync_WithRetryEnabled_RetriesAndSucceeds()
    {
        var provider = CreateProvider(new ExternalDependencyResilienceOptions
        {
            PythonToolExecution = new DependencyResiliencePolicyOptions
            {
                Enabled = true,
                RetryCount = 1,
                RetryBaseDelayMilliseconds = 1,
                TimeoutMilliseconds = 5_000,
                ExceptionsAllowedBeforeBreaking = 5,
                CircuitBreakerDurationSeconds = 30
            }
        });

        var attempts = 0;
        var result = await provider.ExecuteAsync(
            ExternalDependency.PythonToolExecution,
            _ =>
            {
                attempts++;
                if (attempts == 1)
                    throw new InvalidOperationException("transient failure");

                return Task.FromResult("ok");
            });

        Assert.Equal("ok", result);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task ExecuteAsync_WithTimeoutEnabled_ThrowsTimeoutRejectedException()
    {
        var provider = CreateProvider(new ExternalDependencyResilienceOptions
        {
            PythonToolExecution = new DependencyResiliencePolicyOptions
            {
                Enabled = true,
                RetryCount = 0,
                RetryBaseDelayMilliseconds = 1,
                TimeoutMilliseconds = 10,
                ExceptionsAllowedBeforeBreaking = 5,
                CircuitBreakerDurationSeconds = 30
            }
        });

        await Assert.ThrowsAsync<TimeoutRejectedException>(async () =>
            await provider.ExecuteAsync(
                ExternalDependency.PythonToolExecution,
                async cancellationToken =>
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
                }));
    }

    [Fact]
    public async Task HttpPolicy_WithRetryEnabled_RetriesTransientStatusCode()
    {
        var provider = CreateProvider(new ExternalDependencyResilienceOptions
        {
            Ollama = new DependencyResiliencePolicyOptions
            {
                Enabled = true,
                RetryCount = 1,
                RetryBaseDelayMilliseconds = 1,
                TimeoutMilliseconds = 5_000,
                ExceptionsAllowedBeforeBreaking = 5,
                CircuitBreakerDurationSeconds = 30
            }
        });

        var attempts = 0;
        var policy = provider.GetHttpPolicy(ExternalDependency.Ollama);
        var response = await policy.ExecuteAsync(() =>
        {
            attempts++;
            if (attempts == 1)
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError));

            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        });

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, attempts);
    }

    private static PollyExternalDependencyResiliencePolicyProvider CreateProvider(ExternalDependencyResilienceOptions options)
    {
        return new PollyExternalDependencyResiliencePolicyProvider(
            Options.Create(options),
            NullLogger<PollyExternalDependencyResiliencePolicyProvider>.Instance);
    }
}
