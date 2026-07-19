using Embedding.DotNet;
using Microsoft.AspNetCore.Mvc;
using Moq;
using WebApi.DotNet.Contracts.Requests;
using WebApi.DotNet.Controllers;
using Xunit;

namespace WebApi.DotNet.UnitTests;

public class EmbeddingsControllerTests
{
    [Fact]
    public async Task GenerateEmbedding_WithValidRequestReturnsEmbeddingPayload()
    {
        var service = new Mock<IEmbeddingService>();
        service.Setup(candidate => candidate.GenerateEmbeddingAsync("Chicken curry", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResponse("test-model", 3, new[] { 0.1f, 0.2f, 0.3f }));
        var controller = new EmbeddingsController(service.Object);

        var result = await controller.GenerateEmbedding(new EmbeddingRequest { Text = "Chicken curry" }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<WebApi.DotNet.Contracts.Responses.EmbeddingResponse>(ok.Value);
        Assert.Equal("test-model", response.Model);
        Assert.Equal(3, response.Dimensions);
        Assert.Equal(3, response.Embedding.Count);
    }

    [Fact]
    public async Task GenerateEmbedding_WithBlankTextReturnsBadRequest()
    {
        var controller = new EmbeddingsController(Mock.Of<IEmbeddingService>());

        var result = await controller.GenerateEmbedding(new EmbeddingRequest { Text = " " }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }
}