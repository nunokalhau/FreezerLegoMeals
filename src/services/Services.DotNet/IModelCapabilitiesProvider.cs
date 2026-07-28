using System.Net;

namespace Services.DotNet;

public interface IModelCapabilitiesProvider
{
    Task<ModelCapabilities> GetCapabilitiesAsync(string model, CancellationToken cancellationToken = default);

    Task RecordChatResultAsync(
        string model,
        bool toolsWereRequested,
        HttpStatusCode statusCode,
        bool requestSucceeded,
        string? responseBody,
        CancellationToken cancellationToken = default);
}
