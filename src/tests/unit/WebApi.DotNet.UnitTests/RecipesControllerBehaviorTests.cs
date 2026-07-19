using System.Collections.Generic;
using System.Threading.Tasks;
using Domain.DotNet;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Services.DotNet;
using Services.DotNet.Contracts;
using WebApi.DotNet.Controllers;
using WebApi.DotNet.Contracts.Requests;
using Xunit;

namespace WebApi.DotNet.UnitTests;

public class RecipesControllerBehaviorTests
{
    [Fact]
    public async Task SearchRecipesByIngredients_WithNullRequest_ReturnsBadRequest()
    {
        var service = new Mock<IMealService>();
        var controller = new RecipesController(service.Object);

        var result = await controller.SearchRecipesByIngredients(null);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetRecipeById_WithMissingRecipe_ReturnsNotFound()
    {
        var service = new Mock<IMealService>();
        service.Setup(x => x.GetRecipeByIdAsync(55)).ReturnsAsync((Recipe?)null);
        var controller = new RecipesController(service.Object);

        var result = await controller.GetRecipeById(new GetRecipeByIdRequest { Id = 55 });

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetRecipeDetails_WhenServiceReturnsNullRecipe_ReturnsNotFound()
    {
        var service = new Mock<IMealService>();
        service
            .Setup(x => x.GetRecipeDetailsAsync(7))
            .ReturnsAsync(new RecipeDetailsResponse { Error = "No recipe found with ID 7" });
        var controller = new RecipesController(service.Object);

        var result = await controller.GetRecipeDetails(new GetRecipeByIdRequest { Id = 7 });

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task FindMealsWithIngredients_WithValidRequest_MapsServiceResponse()
    {
        var service = new Mock<IMealService>();
        service
            .Setup(x => x.FindMealsWithIngredientsAsync("chicken and rice"))
            .ReturnsAsync(new IngredientSearchResponse
            {
                Query = "chicken and rice",
                SearchTerms = new[] { "chicken", "rice" },
                Recipes = new[] { new Recipe { Id = 1, Name = "Chicken Rice" } },
                TotalRecipesFound = 1,
                Message = "Found 1 recipe"
            });

        var controller = new RecipesController(service.Object);

        var result = await controller.FindMealsWithIngredients(
            new FindMealsWithIngredientsRequest { Query = "chicken and rice" }
        );

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        dynamic payload = ok.Value!;
        Assert.Equal(1, (int)payload.TotalRecipesFound);
    }
}
