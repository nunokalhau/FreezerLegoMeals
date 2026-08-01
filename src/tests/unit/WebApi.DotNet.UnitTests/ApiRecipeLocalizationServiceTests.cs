using Domain.DotNet;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using Repository.DotNet;
using Services.DotNet;
using WebApi.DotNet.Contracts.Requests;
using WebApi.DotNet.Services;
using Xunit;

namespace WebApi.DotNet.UnitTests;

public sealed class ApiRecipeLocalizationServiceTests
{
    [Fact]
    public async Task GetLocalizedRecipeByIdAsync_Prefers_Explicit_Language_Over_AcceptLanguage()
    {
        var queryService = new Mock<ILocalizedRecipeQueryService>();
        LocalizationOptions? initialOptions = null;

        queryService
            .Setup(service => service.GetLocalizedRecipeByIdAsync(It.IsAny<int>(), It.IsAny<LocalizationOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int recipeId, LocalizationOptions options, CancellationToken _) =>
            {
                if (!options.StrictMode)
                {
                    initialOptions = options;
                    return new LocalizedRecipe
                    {
                        CanonicalRecipeId = recipeId,
                        Language = "pt",
                        Name = "Frango Salsa Verde",
                        FallbackLanguageUsed = null,
                        Ingredients = Array.Empty<LocalizedRecipeIngredient>()
                    };
                }

                if (options.PreferredLanguage.Equals("pt", StringComparison.OrdinalIgnoreCase))
                {
                    return new LocalizedRecipe
                    {
                        CanonicalRecipeId = recipeId,
                        Language = "pt",
                        Name = "Frango Salsa Verde",
                        Ingredients = Array.Empty<LocalizedRecipeIngredient>()
                    };
                }

                return null;
            });

        var service = new ApiRecipeLocalizationService(
            queryService.Object,
            new LanguageContextResolver(),
            new LocalizationOptionsFactory(),
            Options.Create(new ApiLocalizationOptions
            {
                DefaultLanguage = "en",
                SupportedLanguages = ["en", "pt"]
            }));

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.AcceptLanguage = "es-ES, en-US;q=0.8";

        var result = await service.GetLocalizedRecipeByIdAsync(
            2,
            new LocalizedRecipeQueryRequest { Language = "pt" },
            httpContext.Request,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.NotNull(initialOptions);
        Assert.Equal("pt", initialOptions!.PreferredLanguage);
        Assert.Equal(new[] { "es-ES", "en-US", "en" }, initialOptions.FallbackLanguages);
        Assert.Equal("pt", result!.Localization.ResolvedLanguage);
    }

    [Fact]
    public async Task GetLocalizedRecipeByIdAsync_Uses_AcceptLanguage_When_Explicit_Missing()
    {
        var queryService = new Mock<ILocalizedRecipeQueryService>();
        LocalizationOptions? initialOptions = null;

        queryService
            .Setup(service => service.GetLocalizedRecipeByIdAsync(It.IsAny<int>(), It.IsAny<LocalizationOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int recipeId, LocalizationOptions options, CancellationToken _) =>
            {
                if (!options.StrictMode)
                {
                    initialOptions = options;
                    return new LocalizedRecipe
                    {
                        CanonicalRecipeId = recipeId,
                        Language = "pt",
                        Name = "Frango Salsa Verde",
                        FallbackLanguageUsed = "pt",
                        Ingredients = Array.Empty<LocalizedRecipeIngredient>()
                    };
                }

                return options.PreferredLanguage.Equals("pt", StringComparison.OrdinalIgnoreCase)
                    ? new LocalizedRecipe
                    {
                        CanonicalRecipeId = recipeId,
                        Language = "pt",
                        Name = "Frango Salsa Verde",
                        Ingredients = Array.Empty<LocalizedRecipeIngredient>()
                    }
                    : null;
            });

        var service = new ApiRecipeLocalizationService(
            queryService.Object,
            new LanguageContextResolver(),
            new LocalizationOptionsFactory(),
            Options.Create(new ApiLocalizationOptions
            {
                DefaultLanguage = "en",
                SupportedLanguages = ["en", "pt", "de"]
            }));

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.AcceptLanguage = "de, pt;q=0.8";

        var result = await service.GetLocalizedRecipeByIdAsync(
            2,
            new LocalizedRecipeQueryRequest(),
            httpContext.Request,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.NotNull(initialOptions);
        Assert.Equal("de", initialOptions!.PreferredLanguage);
        Assert.Equal(new[] { "pt", "en" }, initialOptions.FallbackLanguages);
        Assert.Equal("pt", result!.Localization.ResolvedLanguage);
        Assert.Equal("pt", result.Localization.FallbackLanguageUsed);
        Assert.Contains("pt", result.Localization.AvailableLanguages);
    }

    [Fact]
    public async Task GetLocalizedRecipeByIdAsync_Uses_Default_Language_When_No_Explicit_Or_Header()
    {
        var queryService = new Mock<ILocalizedRecipeQueryService>();
        LocalizationOptions? initialOptions = null;

        queryService
            .Setup(service => service.GetLocalizedRecipeByIdAsync(It.IsAny<int>(), It.IsAny<LocalizationOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int recipeId, LocalizationOptions options, CancellationToken _) =>
            {
                if (!options.StrictMode)
                {
                    initialOptions = options;
                    return new LocalizedRecipe
                    {
                        CanonicalRecipeId = recipeId,
                        Language = "en",
                        Name = "Salsa Verde Chicken",
                        Ingredients = Array.Empty<LocalizedRecipeIngredient>()
                    };
                }

                return options.PreferredLanguage.Equals("en", StringComparison.OrdinalIgnoreCase)
                    ? new LocalizedRecipe
                    {
                        CanonicalRecipeId = recipeId,
                        Language = "en",
                        Name = "Salsa Verde Chicken",
                        Ingredients = Array.Empty<LocalizedRecipeIngredient>()
                    }
                    : null;
            });

        var service = new ApiRecipeLocalizationService(
            queryService.Object,
            new LanguageContextResolver(),
            new LocalizationOptionsFactory(),
            Options.Create(new ApiLocalizationOptions
            {
                DefaultLanguage = "en",
                SupportedLanguages = ["en", "pt"]
            }));

        var httpContext = new DefaultHttpContext();

        var result = await service.GetLocalizedRecipeByIdAsync(
            2,
            new LocalizedRecipeQueryRequest(),
            httpContext.Request,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.NotNull(initialOptions);
        Assert.Equal("en", initialOptions!.PreferredLanguage);
        Assert.Empty(initialOptions.FallbackLanguages);
        Assert.Equal("en", result!.Localization.ResolvedLanguage);
    }
}
