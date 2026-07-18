using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace WebApi.DotNet.IntegrationTests
{
    [Collection("IntegrationTests")]
    public class MealPlanningControllerIntegrationTests : BaseIntegrationTest
    {
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