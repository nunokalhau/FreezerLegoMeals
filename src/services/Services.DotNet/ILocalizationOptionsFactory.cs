using Domain.DotNet;

namespace Services.DotNet;

public interface ILocalizationOptionsFactory
{
    LocalizationOptions Create(LanguageContext context);
}
