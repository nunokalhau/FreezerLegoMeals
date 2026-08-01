namespace Services.DotNet;

public sealed class AssistantLocalizationDefaultsOptions
{
    public string DefaultLanguage { get; set; } = "en";

    public List<string> SupportedLanguages { get; set; } = ["en", "pt"];
}
