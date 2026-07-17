using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FreezerLegoMeals.Domain.DotNet;
using FreezerLegoMeals.WebApi.DotNet.Contracts.Requests;
using FreezerLegoMeals.WebApi.DotNet.Contracts.Responses;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FreezerLegoMeals.WebApi.DotNet.IntegrationTests
{
    [Collection("IntegrationTests")]
    public class ShoppingControllerIntegrationTests : BaseIntegrationTest
    {
        public ShoppingControllerIntegrationTests() : base()
        {
            // Seed the database with test data for each test run
            var options = new DbContextOptionsBuilder<FreezerLegoMealsContext>()
                .UseInMemoryDatabase(databaseName: "TestDatabase")
                .Options;
            
            using var context = new FreezerLegoMealsContext(options);
            IntegrationTestDbSeeder.SeedTestData(context);
        }

        [Fact]
        public async Task GetRecipeIngredients_With_Valid_Identifier_Returns_Success()
        {
            // Arrange
            var getRequest = new GetRecipeRequest
            {
                Identifier = "1"  // Using recipe ID
            };

            // Act
            var response = await _client.GetAsync($"/api/shopping/ingredients/{getRequest.Identifier}");

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal(200, (int)response.StatusCode);
            
            // Verify content type
            Assert.Contains("application/json", response.Content.Headers.ContentType?.ToString());

            // Parse and check response body
            var jsonResponse = await response.Content.ReadAsStringAsync();
            var responseObject = JsonSerializer.Deserialize<GetRecipeIngredientsResponse>(jsonResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            Assert.NotNull(responseObject);
            Assert.NotNull(responseObject.Ingredients);
            Assert.NotEmpty(responseObject.Ingredients);
            Assert.True(responseObject.Found);
            Assert.Equal("1", responseObject.RecipeName); // This would be the identifier used in the request
        }

        [Fact]
        public async Task GetRecipeIngredients_With_Invalid_Identifier_Returns_Success()
        {
            // Arrange
            var getRequest = new GetRecipeRequest
            {
                Identifier = "999"  // Invalid recipe ID
            };

            // Act
            var response = await _client.GetAsync($"/api/shopping/ingredients/{getRequest.Identifier}");

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal(200, (int)response.StatusCode);
            
            // Parse and check response body - this should return an empty list but still 200 OK
            var jsonResponse = await response.Content.ReadAsStringAsync();
            var responseObject = JsonSerializer.Deserialize<GetRecipeIngredientsResponse>(jsonResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            Assert.NotNull(responseObject);
            Assert.NotNull(responseObject.Ingredients);
            Assert.Empty(responseObject.Ingredients);
            Assert.False(responseObject.Found);
        }

        [Fact]
        public async Task GetMultipleRecipeIngredients_With_Valid_Recipe_Identifiers_Returns_Success()
        {
            // Arrange
            var recipeIdentifiers = new List<string> { "1", "2" };

            // Act
            var response = await _client.PostAsJsonAsync("/api/shopping/ingredients", recipeIdentifiers);

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal(200, (int)response.StatusCode);
            
            // Verify content type
            Assert.Contains("application/json", response.Content.Headers.ContentType?.ToString());

            // Parse and check response body
            var jsonResponse = await response.Content.ReadAsStringAsync();
            var responseObject = JsonSerializer.Deserialize<GetMultipleRecipeIngredientsResponse>(jsonResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            Assert.NotNull(responseObject);
            Assert.NotNull(responseObject.RecipeIngredients);
            Assert.NotEmpty(responseObject.RecipeIngredients);
            Assert.True(responseObject.Found);
        }

        [Fact]
        public async Task GetMultipleRecipeIngredients_With_Empty_Request_Returns_BadRequest()
        {
            // Arrange
            var recipeIdentifiers = new List<string>();

            // Act
            var response = await _client.PostAsJsonAsync("/api/shopping/ingredients", recipeIdentifiers);

            // Assert
            Assert.Equal(400, (int)response.StatusCode);
        }

        [Fact]
        public async Task GenerateShoppingList_With_Valid_Recipe_Identifiers_Returns_Success()
        {
            // Arrange
            var generateRequest = new GenerateShoppingListRequest
            {
                RecipeIdentifiers = new List<string> { "1", "2" },
                ScaleFactor = 1.0,
                GroupByCategory = true
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/shopping/generate", generateRequest);

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal(200, (int)response.StatusCode);
            
            // Verify content type
            Assert.Contains("application/json", response.Content.Headers.ContentType?.ToString());

            // Parse and check response body
            var jsonResponse = await response.Content.ReadAsStringAsync();
            var responseObject = JsonSerializer.Deserialize<GenerateShoppingListResponse>(jsonResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            Assert.NotNull(responseObject);
            Assert.NotNull(responseObject.ShoppingList);
            Assert.NotEmpty(responseObject.Message);
        }

        [Fact]
        public async Task GenerateShoppingList_With_Empty_Recipes_Returns_BadRequest()
        {
            // Arrange
            var generateRequest = new GenerateShoppingListRequest
            {
                RecipeIdentifiers = null,
                ScaleFactor = 1.0,
                GroupByCategory = true
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/shopping/generate", generateRequest);

            // Assert
            Assert.Equal(400, (int)response.StatusCode);
        }

        [Fact]
        public async Task GetRecipeInfo_With_Valid_Identifier_Returns_Success()
        {
            // Arrange
            var getRequest = new GetRecipeRequest
            {
                Identifier = "1"  // Using recipe ID
            };

            // Act
            var response = await _client.GetAsync($"/api/shopping/{getRequest.Identifier}/info");

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal(200, (int)response.StatusCode);
            
            // Verify content type
            Assert.Contains("application/json", response.Content.Headers.ContentType?.ToString());

            // Parse and check response body
            var jsonResponse = await response.Content.ReadAsStringAsync();
            var responseObject = JsonSerializer.Deserialize<GetRecipeInfoResponse>(jsonResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            Assert.NotNull(responseObject);
            Assert.NotNull(responseObject.Info);
        }

        [Fact]
        public async Task GetRecipeInfo_With_Invalid_Identifier_Returns_NotFound()
        {
            // Arrange
            var getRequest = new GetRecipeRequest
            {
                Identifier = "999"  // Invalid recipe ID
            };

            // Act
            var response = await _client.GetAsync($"/api/shopping/{getRequest.Identifier}/info");

            // Assert
            Assert.Equal(404, (int)response.StatusCode);
        }
    }
}