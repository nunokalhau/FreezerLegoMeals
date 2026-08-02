namespace Orchestration.DotNet;

public sealed record IntentClassificationResult(
    IntentType Intent,
    double Confidence = 1.0,
    string? MatchedRule = null,
    string? Language = null);