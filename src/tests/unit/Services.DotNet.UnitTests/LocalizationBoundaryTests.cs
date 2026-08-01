using Domain.DotNet;
using Services.DotNet;
using Xunit;

namespace Services.DotNet.UnitTests;

public sealed class LocalizationBoundaryTests
{
    [Fact]
    public void LanguageContextResolver_Uses_ExplicitLanguage_AsPreferredCandidate()
    {
        var resolver = new LanguageContextResolver();

        var context = resolver.Resolve("pt-BR", ["en-US", "es-ES"], "en");

        Assert.Equal("pt-BR", context.ExplicitLanguage);
        Assert.Equal("en", context.DefaultLanguage);
        Assert.Equal(new[] { "en-US", "es-ES" }, context.NegotiatedLanguages);
    }

    [Fact]
    public void LocalizationOptionsFactory_Maps_ExplicitLanguage_AndFallbackChain()
    {
        var factory = new LocalizationOptionsFactory();
        var context = new LanguageContext("pt", ["en", "es", "en"], "en", StrictMode: true);

        var options = factory.Create(context);

        Assert.Equal("pt", options.PreferredLanguage);
        Assert.Equal(new[] { "en", "es" }, options.FallbackLanguages);
        Assert.True(options.StrictMode);
    }

    [Fact]
    public void LocalizationOptionsFactory_Uses_NegotiatedLanguage_WhenExplicitIsMissing()
    {
        var factory = new LocalizationOptionsFactory();
        var context = new LanguageContext(null, ["fr", "en"], "en", StrictMode: false);

        var options = factory.Create(context);

        Assert.Equal("fr", options.PreferredLanguage);
        Assert.Equal(new[] { "en" }, options.FallbackLanguages);
        Assert.False(options.StrictMode);
    }

    [Fact]
    public void LocalizationOptionsFactory_Uses_DefaultLanguage_WhenNoExplicitOrNegotiatedExists()
    {
        var factory = new LocalizationOptionsFactory();
        var context = new LanguageContext(null, Array.Empty<string>(), "en", StrictMode: false);

        var options = factory.Create(context);

        Assert.Equal("en", options.PreferredLanguage);
        Assert.Empty(options.FallbackLanguages);
    }
}
