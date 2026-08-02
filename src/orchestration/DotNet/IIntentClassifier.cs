namespace Orchestration.DotNet;

public interface IIntentClassifier
{
    Task<IntentClassificationResult> ClassifyAsync(
        string message,
        string? preferredLanguage = null,
        CancellationToken cancellationToken = default);
}