namespace Services.DotNet;

public sealed record AssistantLocalizationRequest(
    string? ExplicitLanguage,
    IReadOnlyList<string> NegotiatedLanguages,
    bool StrictMode);
