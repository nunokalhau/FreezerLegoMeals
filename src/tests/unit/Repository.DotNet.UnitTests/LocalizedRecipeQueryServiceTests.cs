using Domain.DotNet;
using Repository.DotNet;
using Xunit;

namespace Repository.DotNet.UnitTests;

public sealed class LocalizedRecipeQueryServiceTests : IDisposable
{
    private readonly InMemoryDbContextFixture _fixture;
    private readonly LocalizedRecipeQueryService _service;

    public LocalizedRecipeQueryServiceTests()
    {
        _fixture = new InMemoryDbContextFixture();
        _service = new LocalizedRecipeQueryService(_fixture.Context);
    }

    public void Dispose()
    {
        _fixture.Dispose();
    }

    [Fact]
    public async Task GetLocalizedRecipesAsync_Returns_LocalizedReadModels_WithPreferredLanguage()
    {
        var options = LocalizationOptions.Create("pt", ["en"]);

        var recipes = await _service.GetLocalizedRecipesAsync(options);

        Assert.NotEmpty(recipes);
        Assert.Contains(recipes, recipe => recipe.CanonicalRecipeId == 1 && recipe.Language == "pt");
        Assert.Contains(recipes, recipe => recipe.CanonicalRecipeId == 2 && recipe.Language == "pt");
        Assert.Equal(new[] { 1, 2, 3 }, recipes.Select(recipe => recipe.CanonicalRecipeId).ToArray());

        var translatedRecipe = recipes.Single(recipe => recipe.CanonicalRecipeId == 1);
        Assert.Equal("Arroz Frito de Frango", translatedRecipe.Name);
        Assert.Contains(translatedRecipe.Ingredients, ingredient => ingredient.Name == "Frango");
    }

    [Fact]
    public async Task GetLocalizedRecipeByIdAsync_Returns_Null_WhenRecipeDoesNotExist()
    {
        var options = LocalizationOptions.Create("en");

        var recipe = await _service.GetLocalizedRecipeByIdAsync(999, options);

        Assert.Null(recipe);
    }

    [Fact]
    public async Task GetLocalizedRecipeByIdAsync_Returns_Ingredients_FromReadPathway()
    {
        var options = LocalizationOptions.Create("pt", ["en"]);

        var recipe = await _service.GetLocalizedRecipeByIdAsync(1, options);

        Assert.NotNull(recipe);
        Assert.Equal(1, recipe!.CanonicalRecipeId);
        Assert.Equal("Arroz Frito de Frango", recipe.Name);
        Assert.Equal("pt", recipe.Language);
        Assert.Null(recipe.FallbackLanguageUsed);
        Assert.Contains(recipe.Ingredients, ingredient => ingredient.Name == "Frango");
        Assert.Contains(recipe.Ingredients, ingredient => ingredient.Name == "Arroz");
    }

    [Fact]
    public async Task GetLocalizedRecipeByIdAsync_Uses_FallbackLanguage_WhenPreferredMissing()
    {
        var options = LocalizationOptions.Create("de", ["es", "pt"]);

        var recipe = await _service.GetLocalizedRecipeByIdAsync(2, options);

        Assert.NotNull(recipe);
        Assert.Equal("Salteado de Res", recipe!.Name);
        Assert.Equal("es", recipe.Language);
        Assert.Equal("es", recipe.FallbackLanguageUsed);
    }

    [Fact]
    public async Task GetLocalizedRecipeByIdAsync_Uses_CanonicalData_WhenNoTranslationExists_AndStrictDisabled()
    {
        var options = LocalizationOptions.Create("de", ["it"], strictMode: false);

        var recipe = await _service.GetLocalizedRecipeByIdAsync(3, options);

        Assert.NotNull(recipe);
        Assert.Equal("Broccoli Beef", recipe!.Name);
        Assert.Equal("de", recipe.Language);
        Assert.Equal("canonical", recipe.FallbackLanguageUsed);
    }

    [Fact]
    public async Task GetLocalizedRecipeByIdAsync_ReturnsNull_WhenStrictModeAndPreferredTranslationMissing()
    {
        var options = LocalizationOptions.Create("de", ["pt"], strictMode: true);

        var recipe = await _service.GetLocalizedRecipeByIdAsync(1, options);

        Assert.Null(recipe);
    }

    [Fact]
    public async Task GetLocalizedRecipesAsync_Excludes_RecipesWithoutPreferredTranslation_InStrictMode()
    {
        var options = LocalizationOptions.Create("pt", strictMode: true);

        var recipes = await _service.GetLocalizedRecipesAsync(options);

        Assert.Equal(new[] { 1, 2 }, recipes.Select(recipe => recipe.CanonicalRecipeId).ToArray());
    }
}
