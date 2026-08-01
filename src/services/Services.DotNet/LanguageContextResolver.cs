using Domain.DotNet;

namespace Services.DotNet;

public sealed class LanguageContextResolver : ILanguageContextResolver
{
    public LanguageContext Resolve(
        string? explicitLanguage,
        IEnumerable<string>? negotiatedLanguages,
        string defaultLanguage,
        bool strictMode = false,
        string? detectedLanguage = null)
    {
        if (string.IsNullOrWhiteSpace(defaultLanguage))
            throw new ArgumentException("Default language is required.", nameof(defaultLanguage));

        var normalizedNegotiated = (negotiatedLanguages ?? Array.Empty<string>())
            .Where(language => !string.IsNullOrWhiteSpace(language))
            .Select(language => language.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new LanguageContext(
            ExplicitLanguage: string.IsNullOrWhiteSpace(explicitLanguage) ? null : explicitLanguage.Trim(),
            NegotiatedLanguages: normalizedNegotiated,
            DefaultLanguage: defaultLanguage.Trim(),
            StrictMode: strictMode,
            DetectedLanguage: string.IsNullOrWhiteSpace(detectedLanguage) ? null : detectedLanguage.Trim());
    }
}
