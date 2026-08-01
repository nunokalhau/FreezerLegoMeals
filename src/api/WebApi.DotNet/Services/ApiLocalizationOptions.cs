namespace WebApi.DotNet.Services;

public sealed class ApiLocalizationOptions
{
    public string DefaultLanguage { get; set; } = "en";

    public List<string> SupportedLanguages { get; set; } = ["en", "pt"];
}
