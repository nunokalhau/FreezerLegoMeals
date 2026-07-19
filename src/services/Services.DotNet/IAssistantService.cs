namespace Services.DotNet;

public interface IAssistantService
{
    Task<string> ChatAsync(string message, CancellationToken cancellationToken = default);
}