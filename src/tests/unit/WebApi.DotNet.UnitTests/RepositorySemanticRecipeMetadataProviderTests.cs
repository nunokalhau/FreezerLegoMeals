using Domain.DotNet;
using Moq;
using Repository.DotNet;
using WebApi.DotNet.Services;
using Xunit;

namespace WebApi.DotNet.UnitTests;

public class RepositorySemanticRecipeMetadataProviderTests
{
    [Fact]
    public async Task GetMetadataAsync_WithKnownRecipe_BuildsRichMetadataAndCachesResults()
    {
        var repository = new Mock<IRecipeRepository>();
        repository
            .Setup(candidate => candidate.GetRecipesAsync())
            .ReturnsAsync([
                new Recipe
                {
                    Id = 10,
                    Name = "Spicy Chicken",
                    Notes = "Freezer friendly",
                    Tags = "spicy,high-protein",
                    Prepping = "Slice chicken",
                    TimeToPrepare = 45,
                    RecipeIngredients =
                    [
                        new RecipeIngredient { Ingredient = new Ingredient { Name = "Chicken" } },
                        new RecipeIngredient { Ingredient = new Ingredient { Name = "Pepper" } }
                    ]
                }
            ]);

        var provider = new RepositorySemanticRecipeMetadataProvider(repository.Object);

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
        repository.Verify(candidate => candidate.GetRecipesAsync(), Times.Once);
    }

    [Fact]
    public async Task GetMetadataAsync_WithUnknownRecipe_ReturnsFallbackMetadata()
    {
        var repository = new Mock<IRecipeRepository>();
        repository.Setup(candidate => candidate.GetRecipesAsync()).ReturnsAsync(new List<Recipe>());

        var provider = new RepositorySemanticRecipeMetadataProvider(repository.Object);

        var metadata = await provider.GetMetadataAsync("999");

        Assert.Equal("999", metadata.RecipeId);
        Assert.Equal("Recipe 999", metadata.Title);
        Assert.Equal(string.Empty, metadata.MatchedText);
    }

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new RepositorySemanticRecipeMetadataProvider(null!));
    }
}
