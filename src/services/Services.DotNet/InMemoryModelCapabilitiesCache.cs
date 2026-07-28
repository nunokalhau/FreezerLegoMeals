using System.Collections.Concurrent;

namespace Services.DotNet;

public sealed class InMemoryModelCapabilitiesCache : IModelCapabilitiesCache
{
    private readonly ConcurrentDictionary<string, ModelCapabilities> _capabilitiesByModel =
        new(StringComparer.OrdinalIgnoreCase);

    public Task<ModelCapabilities?> GetAsync(string model, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        _capabilitiesByModel.TryGetValue(model.Trim(), out var capabilities);
        return Task.FromResult(capabilities);
    }

    public Task SetAsync(ModelCapabilities capabilities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        _capabilitiesByModel[capabilities.Model.Trim()] = capabilities;
        return Task.CompletedTask;
    }
}
