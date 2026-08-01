namespace Domain.DotNet;

public sealed record LanguageContext(
    string? ExplicitLanguage,
    IReadOnlyList<string> NegotiatedLanguages,
    string DefaultLanguage,
    bool StrictMode,
    string? DetectedLanguage = null)
{
    public static LanguageContext ForDefault(string defaultLanguage = "en", bool strictMode = false)
    {
        if (string.IsNullOrWhiteSpace(defaultLanguage))
            throw new ArgumentException("Default language is required.", nameof(defaultLanguage));

        return new LanguageContext(
            ExplicitLanguage: null,
            NegotiatedLanguages: Array.Empty<string>(),
            DefaultLanguage: defaultLanguage.Trim(),
            StrictMode: strictMode,
            DetectedLanguage: null);
    }
}
