using Microsoft.AspNetCore.Mvc;
using Moq;
using Services.DotNet;
using WebApi.DotNet.Contracts.Requests;
using WebApi.DotNet.Contracts.Responses;
using WebApi.DotNet.Controllers;
using Xunit;

namespace WebApi.DotNet.UnitTests;

public class AssistantControllerTests
{
    [Fact]
    public async Task Chat_WithValidRequestDelegatesToAssistantService()
    {
        // Arrange
        var assistantService = new Mock<IAssistantService>();
        assistantService
            .Setup(service => service.ChatAsync("Hello", "conversation-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssistantChatResult("conversation-1", "assistant response"));
        var controller = new AssistantController(assistantService.Object);

        // Act
        var result = await controller.Chat(new AssistantChatRequest
        {
            Message = "Hello",
            ConversationId = "conversation-1"
        }, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AssistantChatResponse>(okResult.Value);
        Assert.Equal("conversation-1", response.ConversationId);
        Assert.Equal("assistant response", response.Response);
        assistantService.Verify(service => service.ChatAsync("Hello", "conversation-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Chat_WithEmptyMessageReturnsBadRequest()
    {
        // Arrange
        var controller = new AssistantController(Mock.Of<IAssistantService>());

        // Act
        var result = await controller.Chat(new AssistantChatRequest
        {
            Message = " "
        }, CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }
}