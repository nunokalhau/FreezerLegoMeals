namespace Services.DotNet;

public interface IOllamaClient
{
    Task<string> ChatAsync(string? model, string userMessage, CancellationToken cancellationToken = default);
}