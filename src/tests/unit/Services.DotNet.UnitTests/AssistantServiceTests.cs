using Moq;
using Xunit;

namespace Services.DotNet.UnitTests;

public class AssistantServiceTests
{
    [Fact]
    public async Task ChatAsync_DelegatesToOllamaClientAndReturnsAssistantResponse()
    {
        // Arrange
        var ollamaClient = new Mock<IOllamaClient>();
        ollamaClient
            .Setup(client => client.ChatAsync(null, "Hello", It.IsAny<CancellationToken>()))
            .ReturnsAsync("assistant response");
        var service = new AssistantService(ollamaClient.Object);

        // Act
        var result = await service.ChatAsync("Hello", CancellationToken.None);

        // Assert
        Assert.Equal("assistant response", result);
        ollamaClient.Verify(client => client.ChatAsync(null, "Hello", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Constructor_WithNullOllamaClientThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new AssistantService(null!));
    }
}