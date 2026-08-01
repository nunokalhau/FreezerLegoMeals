using Domain.DotNet;

namespace Services.DotNet;

public interface ILanguageContextResolver
{
    LanguageContext Resolve(
        string? explicitLanguage,
        IEnumerable<string>? negotiatedLanguages,
        string defaultLanguage,
    bool strictMode = false,
    string? detectedLanguage = null);
}
