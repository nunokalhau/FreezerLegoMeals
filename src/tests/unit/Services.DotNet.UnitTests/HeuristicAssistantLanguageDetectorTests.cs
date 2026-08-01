using Microsoft.Extensions.Options;
using Services.DotNet;
using Xunit;

namespace Services.DotNet.UnitTests;

public sealed class HeuristicAssistantLanguageDetectorTests
{
    [Fact]
    public void Detect_Returns_Portuguese_For_Portuguese_Query()
    {
        var detector = CreateDetector();

        var language = detector.Detect("Que receitas tens com frango?");

        Assert.Equal("pt", language);
    }

    [Fact]
    public void Detect_Returns_English_For_English_Query()
    {
        var detector = CreateDetector();

        var language = detector.Detect("What recipes do you have with chicken?");

        Assert.Equal("en", language);
    }

    private static HeuristicAssistantLanguageDetector CreateDetector()
    {
        return new HeuristicAssistantLanguageDetector(Options.Create(new AssistantLocalizationDefaultsOptions
        {
            DefaultLanguage = "en",
            SupportedLanguages = ["en", "pt", "es", "de", "fr"]
        }));
    }
}