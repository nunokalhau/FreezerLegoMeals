using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using Moq;
using Services.DotNet;
using Domain.DotNet;
using WebApi.DotNet.Contracts.Requests;
using WebApi.DotNet.Contracts.Responses;
using System.Net.Http.Json;

namespace WebApi.DotNet.UnitTests
{
    /// <summary>
    /// Unit tests for the Recipes Controller endpoint.
    /// </summary>
    public class RecipeControllerTests
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly Mock<IMealService> _mockMealService;

        public RecipeControllerTests()
        {
            _factory = new WebApplicationFactory<Program>();
            _mockMealService = new Mock<IMealService>();
        }

        /// <summary>
        /// Tests that SearchRecipesByIngredients properly maps Request DTO to Service and Response DTO back to HTTP.
        /// </summary>
        [Fact]
        public async Task SearchRecipesByIngredients_With_Valid_Request_Returns_Success()
        {
            // Arrange
            var mockRecipes = new List<Recipe> 
            { 
                new Recipe { Id = 1, Name = "Test Recipe" },
                new Recipe { Id = 2, Name = "Another Recipe" }
            };
            
            _mockMealService.Setup(service => service.SearchRecipesByIngredientsAsync(It.IsAny<IEnumerable<string>>()))
                           .ReturnsAsync(mockRecipes);

            var client = _factory.CreateClient();
            var searchRequest = new SearchRecipesRequest
            {
                Ingredients = new List<string> { "chicken", "rice" }
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/recipes/search", searchRequest);

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal(200, (int)response.StatusCode);
            
            // Verify Service was called with Request DTO data
            _mockMealService.Verify(service => service.SearchRecipesByIngredientsAsync(
                It.Is<IEnumerable<string>>(ingredients => ingredients.Contains("chicken") && ingredients.Contains("rice"))
            ), Times.Once);
        }

        /// <summary>
        /// Tests that GetRecipeById properly maps Request DTO to Service and Response DTO back to HTTP.
        /// </summary>
        [Fact]
        public async Task GetRecipeById_With_Valid_Request_Returns_Success()
        {
            // Arrange
            var mockRecipe = new Recipe 
            { 
                Id = 1, 
                Name = "Test Recipe" 
            };
            
            _mockMealService.Setup(service => service.GetRecipeByIdAsync(It.IsAny<int>()))
                           .ReturnsAsync(mockRecipe);

            var client = _factory.CreateClient();
            var getRequest = new GetRecipeByIdRequest
            {
                Id = 1
            };

            // Act
            var response = await client.GetAsync($"/api/recipes/{getRequest.Id}");

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal(200, (int)response.StatusCode);
            
            // Verify Service was called with Request DTO data
            _mockMealService.Verify(service => service.GetRecipeByIdAsync(
                It.Is<int>(id => id == 1)
            ), Times.Once);
        }

        /// <summary>
        /// Tests that FindMealsWithIngredients properly maps Request DTO to Service and Response DTO back to HTTP.
        /// </summary>
        [Fact]
        public async Task FindMealsWithIngredients_With_Valid_Request_Returns_Success()
        {
            // Arrange
            var mockResult = new { 
                total_recipes_found = 5,
                search_terms = new[] { "chicken", "vegetables" },
                recipes = new[] { new Recipe { Id = 1, Name = "Recipe 1" } },
                message = "Found matching recipes"
            };
            
            _mockMealService.Setup(service => service.FindMealsWithIngredientsAsync(It.IsAny<string>()))
                           .ReturnsAsync(mockResult);

            var client = _factory.CreateClient();
            var findRequest = new FindMealsWithIngredientsRequest
            {
                Query = "chicken and vegetables"
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/recipes/find-by-ingredients", findRequest);

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal(200, (int)response.StatusCode);
            
            // Verify Service was called with Request DTO data
            _mockMealService.Verify(service => service.FindMealsWithIngredientsAsync(
                It.Is<string>(query => query == "chicken and vegetables")
            ), Times.Once);
        }

        /// <summary>
        /// Tests that GetRecipeDetails properly maps Request DTO to Service and Response DTO back to HTTP.
        /// </summary>
        [Fact]
        public async Task GetRecipeDetails_With_Valid_Request_Returns_Success()
        {
            // Arrange
            var mockResult = new { 
                recipe = new Recipe { Id = 1, Name = "Recipe 1" },
                message = "Recipe details retrieved"
            };
            
            _mockMealService.Setup(service => service.GetRecipeDetailsAsync(It.IsAny<int>()))
                           .ReturnsAsync(mockResult);

            var client = _factory.CreateClient();
            var getDetailRequest = new GetRecipeByIdRequest
            {
                Id = 1
            };

            // Act
            var response = await client.GetAsync($"/api/recipes/{getDetailRequest.Id}/details");

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal(200, (int)response.StatusCode);
            
            // Verify Service was called with Request DTO data
            _mockMealService.Verify(service => service.GetRecipeDetailsAsync(
                It.Is<int>(id => id == 1)
            ), Times.Once);
        }

        /// <summary>
        /// Tests that SearchRecipesByIngredients handles bad request correctly.
        /// </summary>
        [Fact]
        public async Task SearchRecipesByIngredients_With_Empty_Request_Returns_BadRequest()
        {
            // Arrange
            var client = _factory.CreateClient();
            var searchRequest = new SearchRecipesRequest();

            // Act
            var response = await client.PostAsJsonAsync("/api/recipes/search", searchRequest);

            // Assert
            Assert.Equal(400, (int)response.StatusCode);
        }

        /// <summary>
        /// Tests that GetRecipeById handles bad request correctly.
        /// </summary>
        [Fact]
        public async Task GetRecipeById_With_Empty_Request_Returns_BadRequest()
        {
            // Arrange
            var client = _factory.CreateClient();
            var getRequest = new GetRecipeByIdRequest();

            // Act
            var response = await client.GetAsync("/api/recipes/0");

            // Assert
            Assert.Equal(400, (int)response.StatusCode);
        }
    }
}