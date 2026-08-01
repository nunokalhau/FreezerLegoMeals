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
        var options = LocalizationOptions.Create("en", ["pt"]);

        var recipes = await _service.GetLocalizedRecipesAsync(options);

        Assert.NotEmpty(recipes);
        Assert.All(recipes, recipe => Assert.Equal("en", recipe.Language));
        Assert.Equal(new[] { 1, 2, 3 }, recipes.Select(recipe => recipe.CanonicalRecipeId).ToArray());
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
        var options = LocalizationOptions.Create("en");

        var recipe = await _service.GetLocalizedRecipeByIdAsync(1, options);

        Assert.NotNull(recipe);
        Assert.Equal(1, recipe!.CanonicalRecipeId);
        Assert.Equal("Chicken Fried Rice", recipe.Name);
        Assert.Equal("en", recipe.Language);
        Assert.Contains(recipe.Ingredients, ingredient => ingredient.Name == "Chicken");
        Assert.Contains(recipe.Ingredients, ingredient => ingredient.Name == "Rice");
    }
}
