namespace Domain.DotNet;

public sealed record LocalizationOptions(
    string PreferredLanguage,
    IReadOnlyList<string> FallbackLanguages,
    bool StrictMode)
{
    public static LocalizationOptions Create(
        string preferredLanguage,
        IEnumerable<string>? fallbackLanguages = null,
        bool strictMode = false)
    {
        if (string.IsNullOrWhiteSpace(preferredLanguage))
            throw new ArgumentException("Preferred language is required.", nameof(preferredLanguage));

        var fallbackChain = (fallbackLanguages ?? Array.Empty<string>())
            .Where(language => !string.IsNullOrWhiteSpace(language))
            .Select(language => language.Trim())
            .Where(language => !language.Equals(preferredLanguage.Trim(), StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new LocalizationOptions(
            PreferredLanguage: preferredLanguage.Trim(),
            FallbackLanguages: fallbackChain,
            StrictMode: strictMode);
    }
}
