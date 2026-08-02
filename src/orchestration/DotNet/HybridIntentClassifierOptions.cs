namespace Orchestration.DotNet;

public sealed class HybridIntentClassifierOptions
{
    // Conservative default to preserve existing behavior unless explicitly tuned.
    public double LowConfidenceThreshold { get; set; } = 0.6;

    public string? Model { get; set; }
}