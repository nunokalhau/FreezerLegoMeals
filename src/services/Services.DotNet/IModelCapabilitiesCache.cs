namespace Services.DotNet;

public interface IModelCapabilitiesCache
{
    Task<ModelCapabilities?> GetAsync(string model, CancellationToken cancellationToken = default);

    Task SetAsync(ModelCapabilities capabilities, CancellationToken cancellationToken = default);
}
