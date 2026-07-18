using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Domain.DotNet;
using WebApi.DotNet.Contracts.Requests;
using WebApi.DotNet.Contracts.Responses;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace WebApi.DotNet.IntegrationTests
{
    [Collection("IntegrationTests")]
    public class RecipesControllerIntegrationTests : BaseIntegrationTest
    {
        public RecipesControllerIntegrationTests() : base()
        {
            // Seed the database with test data for each test run
            var options = new DbContextOptionsBuilder<FreezerLegoMealsContext>()
                .UseInMemoryDatabase(databaseName: "TestDatabase")
                .Options;
            
            using var context = new FreezerLegoMealsContext(options);
            IntegrationTestDbSeeder.SeedTestData(context);
        }

        [Fact]
        public async Task SearchRecipesByIngredients_With_Valid_Request_Returns_Success()
        {
            // Arrange
            var searchRequest = new SearchRecipesRequest
            {
                Ingredients = new List<string> { "chicken", "rice" }
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/recipes/search", searchRequest);

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal(200, (int)response.StatusCode);
            
            // Verify content type
            Assert.Contains("application/json", response.Content.Headers.ContentType?.ToString());

            // Parse and check response body
            var jsonResponse = await response.Content.ReadAsStringAsync();
            var responseObject = JsonSerializer.Deserialize<SearchRecipesResponse>(jsonResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            Assert.NotNull(responseObject);
            Assert.NotNull(responseObject.Recipes);
            Assert.NotEmpty(responseObject.Recipes);
            Assert.Equal(1, responseObject.TotalRecipesFound); // Should find Chicken Fried Rice
            
            var recipe = responseObject.Recipes.First();
            Assert.Equal("Chicken Fried Rice", recipe.Name);
        }

        [Fact]
        public async Task SearchRecipesByIngredients_With_Empty_Request_Returns_BadRequest()
        {
            // Arrange
            var searchRequest = new SearchRecipesRequest();

            // Act
            var response = await _client.PostAsJsonAsync("/api/recipes/search", searchRequest);

            // Assert
            Assert.Equal(400, (int)response.StatusCode);
        }

        [Fact]
        public async Task SearchRecipesByIngredients_With_No_Ingredients_Returns_BadRequest()
        {
            // Arrange
            var searchRequest = new SearchRecipesRequest
            {
                Ingredients = null
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/recipes/search", searchRequest);

            // Assert
            Assert.Equal(400, (int)response.StatusCode);
        }

        [Fact]
        public async Task GetRecipeById_With_Valid_Id_Returns_Success()
        {
            // Arrange
            var getRequest = new GetRecipeByIdRequest
            {
                Id = 1
            };

            // Act
            var response = await _client.GetAsync($"/api/recipes/{getRequest.Id}");

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal(200, (int)response.StatusCode);
            
            // Verify content type
            Assert.Contains("application/json", response.Content.Headers.ContentType?.ToString());

            // Parse and check response body
            var jsonResponse = await response.Content.ReadAsStringAsync();
            var responseObject = JsonSerializer.Deserialize<GetRecipeByIdResponse>(jsonResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            Assert.NotNull(responseObject);
            Assert.NotNull(responseObject.Recipe);
            Assert.True(responseObject.Found);
            Assert.Equal("Chicken Fried Rice", responseObject.Recipe.Name);
        }

        [Fact]
        public async Task GetRecipeById_With_Invalid_Id_Returns_NotFound()
        {
            // Arrange
            var getRequest = new GetRecipeByIdRequest
            {
                Id = 999
            };

            // Act
            var response = await _client.GetAsync($"/api/recipes/{getRequest.Id}");

            // Assert
            Assert.Equal(404, (int)response.StatusCode);
        }

        [Fact]
        public async Task FindMealsWithIngredients_With_Valid_Query_Returns_Success()
        {
            // Arrange
            var findRequest = new FindMealsWithIngredientsRequest
            {
                Query = "chicken and rice"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/recipes/find-by-ingredients", findRequest);

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal(200, (int)response.StatusCode);
            
            // Verify content type
            Assert.Contains("application/json", response.Content.Headers.ContentType?.ToString());

            // Parse and check response body
            var jsonResponse = await response.Content.ReadAsStringAsync();
            var responseObject = JsonSerializer.Deserialize<FindMealsWithIngredientsResponse>(jsonResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            Assert.NotNull(responseObject);
            Assert.Equal("chicken and rice", responseObject.Query);
            Assert.NotEmpty(responseObject.Recipes);
            Assert.Equal(1, responseObject.TotalRecipesFound); // Should find Chicken Fried Rice
        }

        [Fact]
        public async Task FindMealsWithIngredients_With_Empty_Query_Returns_BadRequest()
        {
            // Arrange
            var findRequest = new FindMealsWithIngredientsRequest
            {
                Query = ""
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/recipes/find-by-ingredients", findRequest);

            // Assert
            Assert.Equal(400, (int)response.StatusCode);
        }

        [Fact]
        public async Task GetRecipeDetails_With_Valid_Id_Returns_Success()
        {
            // Arrange
            var getDetailRequest = new GetRecipeByIdRequest
            {
                Id = 1
            };

            // Act
            var response = await _client.GetAsync($"/api/recipes/{getDetailRequest.Id}/details");

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal(200, (int)response.StatusCode);
            
            // Verify content type
            Assert.Contains("application/json", response.Content.Headers.ContentType?.ToString());

            // Parse and check response body
            var jsonResponse = await response.Content.ReadAsStringAsync();
            var responseObject = JsonSerializer.Deserialize<GetRecipeDetailsResponse>(jsonResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            Assert.NotNull(responseObject);
            Assert.NotNull(responseObject.Recipe); 
            Assert.True(responseObject.Found);
            Assert.Equal("Chicken Fried Rice", responseObject.Recipe.Name);
        }

        [Fact]
        public async Task GetRecipeDetails_With_Invalid_Id_Returns_NotFound()
        {
            // Arrange
            var getDetailRequest = new GetRecipeByIdRequest
            {
                Id = 999
            };

            // Act
            var response = await _client.GetAsync($"/api/recipes/{getDetailRequest.Id}/details");

            // Assert
            Assert.Equal(404, (int)response.StatusCode);
        }
    }
}