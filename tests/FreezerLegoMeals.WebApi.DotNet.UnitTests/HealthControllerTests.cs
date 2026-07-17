using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using Newtonsoft.Json;

namespace FreezerLegoMeals.WebApi.DotNet.UnitTests
{
    /// <summary>
    /// Unit tests for the Health Controller endpoint.
    /// </summary>
    public class HealthControllerTests
    {
        private readonly WebApplicationFactory<Program> _factory;

        public HealthControllerTests()
        {
            _factory = new WebApplicationFactory<Program>();
        }

        /// <summary>
        /// Tests that the health endpoint returns a successful response with expected structure.
        /// </summary>
        [Fact]
        public async Task Get_Health_Returns_Success()
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act
            var response = await client.GetAsync("/api/health");

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        /// <summary>
        /// Tests that the health endpoint returns correct JSON structure.
        /// </summary>
        [Fact]
        public async Task Get_Health_Returns_Correct_Json_Response()
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act
            var response = await client.GetAsync("/api/health");
            var responseContent = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.NotNull(responseContent);
            Assert.Contains("healthy", responseContent);
            Assert.Contains("FreezerLegoMeals.WebApi.DotNet", responseContent);
        }

        /// <summary>
        /// Tests the detailed health endpoint response structure.
        /// </summary>
        [Fact]
        public async Task Get_Health_Returns_Correct_Structure()
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act
            var response = await client.GetAsync("/api/health");
            var responseContent = await response.Content.ReadAsStringAsync();
            var jsonResponse = JsonConvert.DeserializeObject<HealthResponse>(responseContent);

            // Assert
            Assert.NotNull(jsonResponse);
            Assert.Equal("healthy", jsonResponse.status);
            Assert.Equal("FreezerLegoMeals.WebApi.DotNet", jsonResponse.service);
        }

        /// <summary>
        /// Test class for the health response structure.
        /// </summary>
        public class HealthResponse
        {
            public string status { get; set; }
            public string service { get; set; }
        }
    }
}