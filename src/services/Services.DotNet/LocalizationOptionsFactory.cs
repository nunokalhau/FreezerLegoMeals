using Domain.DotNet;

namespace Services.DotNet;

public sealed class LocalizationOptionsFactory : ILocalizationOptionsFactory
{
    public LocalizationOptions Create(LanguageContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var preferredLanguage = ResolvePreferredLanguage(context);

        var fallbackChain = context.NegotiatedLanguages
            .Append(context.DefaultLanguage)
            .Where(language => !string.IsNullOrWhiteSpace(language))
            .Select(language => language.Trim())
            .Where(language => !language.Equals(preferredLanguage, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return LocalizationOptions.Create(preferredLanguage, fallbackChain, context.StrictMode);
    }

    private static string ResolvePreferredLanguage(LanguageContext context)
    {
        if (!string.IsNullOrWhiteSpace(context.ExplicitLanguage))
            return context.ExplicitLanguage.Trim();

        if (!string.IsNullOrWhiteSpace(context.DetectedLanguage))
            return context.DetectedLanguage.Trim();

        var negotiated = context.NegotiatedLanguages.FirstOrDefault(language => !string.IsNullOrWhiteSpace(language));
        if (!string.IsNullOrWhiteSpace(negotiated))
            return negotiated.Trim();

        if (!string.IsNullOrWhiteSpace(context.DefaultLanguage))
            return context.DefaultLanguage.Trim();

        throw new ArgumentException("Default language is required in language context.", nameof(context));
    }
}
