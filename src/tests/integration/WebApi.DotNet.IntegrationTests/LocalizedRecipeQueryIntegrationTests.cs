using Domain.DotNet;
using Microsoft.Extensions.DependencyInjection;
using Repository.DotNet;
using Xunit;

namespace WebApi.DotNet.IntegrationTests;

[Collection("IntegrationTests")]
public sealed class LocalizedRecipeQueryIntegrationTests : BaseIntegrationTest
{
    [Fact]
    public async Task QueryService_UsesPreferredLanguage_WhenTranslationExists()
    {
        using var scope = _factory.Services.CreateScope();
        var queryService = scope.ServiceProvider.GetRequiredService<ILocalizedRecipeQueryService>();
        var options = LocalizationOptions.Create("pt", ["en"]);

        var recipe = await queryService.GetLocalizedRecipeByIdAsync(1, options);

        Assert.NotNull(recipe);
        Assert.Equal("Arroz Frito de Frango", recipe!.Name);
        Assert.Equal("pt", recipe.Language);
        Assert.Null(recipe.FallbackLanguageUsed);
    }

    [Fact]
    public async Task QueryService_UsesFallbackLanguage_WhenPreferredTranslationMissing()
    {
        using var scope = _factory.Services.CreateScope();
        var queryService = scope.ServiceProvider.GetRequiredService<ILocalizedRecipeQueryService>();
        var options = LocalizationOptions.Create("de", ["pt"]);

        var recipe = await queryService.GetLocalizedRecipeByIdAsync(2, options);

        Assert.NotNull(recipe);
        Assert.Equal("Salteado de Carne", recipe!.Name);
        Assert.Equal("pt", recipe.Language);
        Assert.Equal("pt", recipe.FallbackLanguageUsed);
    }

    [Fact]
    public async Task QueryService_ReturnsNull_WhenStrictModeAndPreferredTranslationMissing()
    {
        using var scope = _factory.Services.CreateScope();
        var queryService = scope.ServiceProvider.GetRequiredService<ILocalizedRecipeQueryService>();
        var options = LocalizationOptions.Create("de", ["pt"], strictMode: true);

        var recipe = await queryService.GetLocalizedRecipeByIdAsync(1, options);

        Assert.Null(recipe);
    }
}
