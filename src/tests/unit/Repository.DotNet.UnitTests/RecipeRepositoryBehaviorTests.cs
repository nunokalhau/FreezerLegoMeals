using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Repository.DotNet.UnitTests;

public class RecipeRepositoryBehaviorTests : IDisposable
{
    private readonly InMemoryDbContextFixture _fixture;
    private readonly RecipeRepository _repository;

    public RecipeRepositoryBehaviorTests()
    {
        _fixture = new InMemoryDbContextFixture();
        _repository = new RecipeRepository(_fixture.Context);
    }

    [Fact]
    public async Task FindRecipesWithIngredients_WithNullIngredients_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _repository.FindRecipesWithIngredientsAsync(null));
    }

    [Fact]
    public async Task FindRecipesWithIngredients_IgnoresWhitespace_AndMatchesCaseInsensitive()
    {
        var results = await _repository.FindRecipesWithIngredientsAsync(new[] { "  chicken  ", "   " });

        var list = results.ToList();
        Assert.Single(list);
        Assert.Equal("Chicken Fried Rice", list[0].Name);
    }

    [Fact]
    public async Task GetIngredientByName_WithWhitespaceOnly_ReturnsNull()
    {
        var result = await _repository.GetIngredientByNameAsync("   ");

        Assert.Null(result);
    }

    public void Dispose()
    {
        _fixture.Dispose();
    }
}
