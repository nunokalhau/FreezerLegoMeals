using Domain.DotNet;
using Microsoft.AspNetCore.Mvc.Testing;
using Moq;
using Services.DotNet;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Threading.Tasks;
using WebApi.DotNet.Contracts.Requests;
using WebApi.DotNet.Contracts.Responses;
using Xunit;

namespace WebApi.DotNet.UnitTests
{
    /// <summary>
    /// Unit tests for the Shopping Controller endpoint.
    /// </summary>
    public class ShoppingControllerTests
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly Mock<IShoppingService> _mockShoppingService;

        public ShoppingControllerTests()
        {
            _factory = new WebApplicationFactory<Program>();
            _mockShoppingService = new Mock<IShoppingService>();
        }

        /// <summary>
        /// Tests that GetRecipeIngredients properly maps Request DTO to Service and Response DTO back to HTTP.
        /// </summary>
        [Fact]
        public async Task GetRecipeIngredients_With_Valid_Request_Returns_Success()
        {
            // Arrange
            var mockIngredients = new List<RecipeIngredient> 
            { 
                new RecipeIngredient { RecipeId = 1, IngredientId = 1, Amount = 2.0, Unit = "cups" },
                new RecipeIngredient { RecipeId = 1, IngredientId = 2, Amount = 1.0, Unit = "tbsp" }
            };
            
            _mockShoppingService.Setup(service => service.GetRecipeIngredientsAsync(It.IsAny<string>()))
                               .ReturnsAsync(mockIngredients);

            var client = _factory.CreateClient();
            var getRequest = new GetRecipeRequest
            {
                Identifier = "test-recipe"
            };

            // Act
            var response = await client.GetAsync($"/api/shopping/ingredients/{getRequest.Identifier}");

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal(200, (int)response.StatusCode);
            
            // Verify Service was called with Request DTO data
            _mockShoppingService.Verify(service => service.GetRecipeIngredientsAsync(
                It.Is<string>(identifier => identifier == "test-recipe")
            ), Times.Once);
        }

        /// <summary>
        /// Tests that GetMultipleRecipeIngredients properly maps Request DTO to Service and Response DTO back to HTTP.
        /// </summary>
        [Fact]
        public async Task GetMultipleRecipeIngredients_With_Valid_Request_Returns_Success()
        {
            // Arrange
            var mockIngredients = new Dictionary<string, IEnumerable<RecipeIngredient>>
            {
                { "recipe1", new List<RecipeIngredient> { new RecipeIngredient { RecipeId = 1, IngredientId = 1 } } },
                { "recipe2", new List<RecipeIngredient> { new RecipeIngredient { RecipeId = 2, IngredientId = 2 } } }
            };
            
            _mockShoppingService.Setup(service => service.GetMultipleRecipeIngredientsAsync(It.IsAny<IEnumerable<string>>()))
                               .ReturnsAsync(mockIngredients);

            var client = _factory.CreateClient();
            var multiRequest = new List<string> { "recipe1", "recipe2" };

            // Act
            var response = await client.PostAsJsonAsync("/api/shopping/ingredients", multiRequest);

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal(200, (int)response.StatusCode);
            
            // Verify Service was called with Request DTO data
            _mockShoppingService.Verify(service => service.GetMultipleRecipeIngredientsAsync(
                It.Is<IEnumerable<string>>(identifiers => identifiers.Contains("recipe1") && identifiers.Contains("recipe2"))
            ), Times.Once);
        }

        /// <summary>
        /// Tests that GenerateShoppingList properly maps Request DTO to Service and Response DTO back to HTTP.
        /// </summary>
        [Fact]
        public async Task GenerateShoppingList_With_Valid_Request_Returns_Success()
        {
            // Arrange
            var mockResult = new { 
                shopping_list = new Dictionary<string, object> { { "category1", new List<object>() } },
                message = "Shopping list generated successfully"
            };
            
            _mockShoppingService.Setup(service => service.GenerateShoppingListAsync(
                It.IsAny<IEnumerable<string>>(), 
                It.IsAny<double>(), 
                It.IsAny<bool>()))
                               .ReturnsAsync(mockResult);

            var client = _factory.CreateClient();
            var generateRequest = new GenerateShoppingListRequest
            {
                RecipeIdentifiers = new List<string> { "recipe1", "recipe2" },
                ScaleFactor = 2.0,
                GroupByCategory = true
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/shopping/generate", generateRequest);

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal(200, (int)response.StatusCode);
            
            // Verify Service was called with Request DTO data
            _mockShoppingService.Verify(service => service.GenerateShoppingListAsync(
                It.Is<IEnumerable<string>>(identifiers => identifiers.Contains("recipe1") && identifiers.Contains("recipe2")),
                It.Is<double>(factor => factor == 2.0),
                It.Is<bool>(groupBy => groupBy == true)
            ), Times.Once);
        }

        /// <summary>
        /// Tests that GetRecipeInfo properly maps Request DTO to Service and Response DTO back to HTTP.
        /// </summary>
        [Fact]
        public async Task GetRecipeInfo_With_Valid_Request_Returns_Success()
        {
            // Arrange
            var mockResult = new { 
                info = new { name = "Test Recipe", servings = 4 }
            };
            
            _mockShoppingService.Setup(service => service.GetRecipeInfoAsync(It.IsAny<string>()))
                               .ReturnsAsync(mockResult);

            var client = _factory.CreateClient();
            var getRequest = new GetRecipeRequest
            {
                Identifier = "test-recipe"
            };

            // Act
            var response = await client.GetAsync($"/api/shopping/{getRequest.Identifier}/info");

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal(200, (int)response.StatusCode);
            
            // Verify Service was called with Request DTO data
            _mockShoppingService.Verify(service => service.GetRecipeInfoAsync(
                It.Is<string>(identifier => identifier == "test-recipe")
            ), Times.Once);
        }

        /// <summary>
        /// Tests that GetRecipeIngredients handles bad request correctly.
        /// </summary>
        [Fact]
        public async Task GetRecipeIngredients_With_Empty_Request_Returns_BadRequest()
        {
            // Arrange
            var client = _factory.CreateClient();
            var getRequest = new GetRecipeRequest();

            // Act
            var response = await client.GetAsync("/api/shopping/ingredients/");

            // Assert
            Assert.Equal(400, (int)response.StatusCode);
        }

        /// <summary>
        /// Tests that GenerateShoppingList handles bad request correctly.
        /// </summary>
        [Fact]
        public async Task GenerateShoppingList_With_Empty_Request_Returns_BadRequest()
        {
            // Arrange
            var client = _factory.CreateClient();
            var generateRequest = new GenerateShoppingListRequest();

            // Act
            var response = await client.PostAsJsonAsync("/api/shopping/generate", generateRequest);

            // Assert
            Assert.Equal(400, (int)response.StatusCode);
        }
    }
}