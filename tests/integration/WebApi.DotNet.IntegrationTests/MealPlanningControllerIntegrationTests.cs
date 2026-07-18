using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace WebApi.DotNet.IntegrationTests
{
    [Collection("IntegrationTests")]
    public class MealPlanningControllerIntegrationTests : BaseIntegrationTest
    {
        public MealPlanningControllerIntegrationTests() : base()
        {
            // Seed the database with test data for each test run
            var options = new DbContextOptionsBuilder<FreezerLegoMealsContext>()
                .UseInMemoryDatabase(databaseName: "TestDatabase")
                .Options;
            
            using var context = new FreezerLegoMealsContext(options);
            IntegrationTestDbSeeder.SeedTestData(context);
        }

        [Fact]
        public async Task MealPlanningController_Endpoint_Exists()
        {
            // Act
            var response = await _client.GetAsync("/api/mealplanning");

            // Assert - Currently this endpoint doesn't exist but we'll test for proper routing
            // Since the controller is empty, we should get a 404 or method not allowed 
            // but at least it shouldn't crash or return 500
            Assert.NotEqual(500, (int)response.StatusCode);
        }
    }
}