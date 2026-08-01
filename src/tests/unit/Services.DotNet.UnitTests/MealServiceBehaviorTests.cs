using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.DotNet;
using Moq;
using Repository.DotNet;
using Xunit;

namespace Services.DotNet.UnitTests;

public class MealServiceBehaviorTests
{
    [Fact]
    public async Task FindMealsWithIngredients_ExtractsQueryTerms_AndQueriesRepository()
    {
        var repo = new Mock<IRecipeRepository>();
        repo.Setup(r => r.FindRecipesWithIngredientsAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new List<Recipe>());

        var service = new MealService(repo.Object);

        await service.FindMealsWithIngredientsAsync("Need Frango and rice for dinner");

        repo.Verify(r => r.FindRecipesWithIngredientsAsync(
            It.Is<IEnumerable<string>>(terms => terms.Contains("frango") && terms.Contains("rice") && terms.Contains("dinner"))
        ), Times.Once);
    }

    [Fact]
    public async Task FindMealsWithIngredients_WithOnlyShortTokens_UsesEmptyTermList()
    {
        var repo = new Mock<IRecipeRepository>();
        repo.Setup(r => r.FindRecipesWithIngredientsAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(Array.Empty<Recipe>());

        var service = new MealService(repo.Object);

        var response = await service.FindMealsWithIngredientsAsync("a to !");

        repo.Verify(r => r.FindRecipesWithIngredientsAsync(
            It.Is<IEnumerable<string>>(terms => !terms.Any())
        ), Times.Once);
        Assert.Equal(0, response.TotalRecipesFound);
    }
}
