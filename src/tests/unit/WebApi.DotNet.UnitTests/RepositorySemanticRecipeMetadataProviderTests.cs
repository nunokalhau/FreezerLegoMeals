using Domain.DotNet;
using Moq;
using Repository.DotNet;
using Services.DotNet;
using WebApi.DotNet.Services;
using Xunit;

namespace WebApi.DotNet.UnitTests;

public class RepositorySemanticRecipeMetadataProviderTests
{
    [Fact]
    public async Task GetMetadataAsync_WithKnownRecipe_BuildsRichMetadataAndCachesResults()
    {
        var queryService = new Mock<ILocalizedRecipeQueryService>();
        queryService
            .Setup(candidate => candidate.GetLocalizedRecipesAsync(It.IsAny<LocalizationOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new LocalizedRecipe
                {
                    CanonicalRecipeId = 10,
                    Language = "en",
                    Name = "Spicy Chicken",
                    Notes = "Freezer friendly",
                    Tags = "spicy,high-protein",
                    Prepping = "Slice chicken",
                    TimeToPrepare = 45,
                    Ingredients =
                    [
                        new LocalizedRecipeIngredient { CanonicalIngredientId = 1, Language = "en", Name = "Chicken" },
                        new LocalizedRecipeIngredient { CanonicalIngredientId = 2, Language = "en", Name = "Pepper" }
                    ]
                }
            ]);

        var optionsFactory = new Mock<ILocalizationOptionsFactory>();
        optionsFactory
            .Setup(factory => factory.Create(It.IsAny<LanguageContext>()))
            .Returns(LocalizationOptions.Create("en"));

        var languageContextResolver = new Mock<ILanguageContextResolver>();
        languageContextResolver
            .Setup(resolver => resolver.Resolve(It.IsAny<string?>(), It.IsAny<IEnumerable<string>?>(), It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(new LanguageContext(null, Array.Empty<string>(), "en", false));

        var provider = new RepositorySemanticRecipeMetadataProvider(queryService.Object, languageContextResolver.Object, optionsFactory.Object);

        var metadata = await provider.GetMetadataAsync("10");
        var cachedMetadata = await provider.GetMetadataAsync("10");

        Assert.Equal("10", metadata.RecipeId);
        Assert.Equal("Spicy Chicken", metadata.Title);
        Assert.Contains("Spicy Chicken", metadata.MatchedText);
        Assert.Contains("Freezer friendly", metadata.MatchedText);
        Assert.Contains("spicy,high-protein", metadata.MatchedText);
        Assert.Contains("Slice chicken", metadata.MatchedText);
        Assert.Contains("Chicken, Pepper", metadata.MatchedText);
        Assert.Equal("Freezer friendly", metadata.Description);
        Assert.Equal("spicy,high-protein", metadata.Tags);
        Assert.Equal(new[] { "Chicken", "Pepper" }, metadata.Ingredients);
        Assert.Equal("Slice chicken", metadata.PreparationSteps);
        Assert.Equal("45", metadata.CookingTime);

        Assert.Equal(metadata, cachedMetadata);
        languageContextResolver.Verify(resolver => resolver.Resolve(It.IsAny<string?>(), It.IsAny<IEnumerable<string>?>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Once);
        optionsFactory.Verify(factory => factory.Create(It.IsAny<LanguageContext>()), Times.Once);
        queryService.Verify(candidate => candidate.GetLocalizedRecipesAsync(It.IsAny<LocalizationOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMetadataAsync_WithUnknownRecipe_ReturnsFallbackMetadata()
    {
        var queryService = new Mock<ILocalizedRecipeQueryService>();
        queryService
            .Setup(candidate => candidate.GetLocalizedRecipesAsync(It.IsAny<LocalizationOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var optionsFactory = new Mock<ILocalizationOptionsFactory>();
        optionsFactory
            .Setup(factory => factory.Create(It.IsAny<LanguageContext>()))
            .Returns(LocalizationOptions.Create("en"));

        var languageContextResolver = new Mock<ILanguageContextResolver>();
        languageContextResolver
            .Setup(resolver => resolver.Resolve(It.IsAny<string?>(), It.IsAny<IEnumerable<string>?>(), It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(new LanguageContext(null, Array.Empty<string>(), "en", false));

        var provider = new RepositorySemanticRecipeMetadataProvider(queryService.Object, languageContextResolver.Object, optionsFactory.Object);

        var metadata = await provider.GetMetadataAsync("999");

        Assert.Equal("999", metadata.RecipeId);
        Assert.Equal("Recipe 999", metadata.Title);
        Assert.Equal(string.Empty, metadata.MatchedText);
    }

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        var queryService = new Mock<ILocalizedRecipeQueryService>();
        var languageContextResolver = new Mock<ILanguageContextResolver>();
        var optionsFactory = new Mock<ILocalizationOptionsFactory>();

        Assert.Throws<ArgumentNullException>(() => new RepositorySemanticRecipeMetadataProvider(null!, languageContextResolver.Object, optionsFactory.Object));
        Assert.Throws<ArgumentNullException>(() => new RepositorySemanticRecipeMetadataProvider(queryService.Object, null!, optionsFactory.Object));
        Assert.Throws<ArgumentNullException>(() => new RepositorySemanticRecipeMetadataProvider(queryService.Object, languageContextResolver.Object, null!));
    }
}
