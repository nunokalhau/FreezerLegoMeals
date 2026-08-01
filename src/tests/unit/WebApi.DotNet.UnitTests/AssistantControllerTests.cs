using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
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
            .Setup(service => service.ChatAsync("Hello", "conversation-1", It.IsAny<AssistantLocalizationRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssistantChatResult("conversation-1", "assistant response"));
        var controller = new AssistantController(assistantService.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

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
        assistantService.Verify(service => service.ChatAsync("Hello", "conversation-1", It.IsAny<AssistantLocalizationRequest?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Chat_WithEmptyMessageReturnsBadRequest()
    {
        // Arrange
        var controller = new AssistantController(Mock.Of<IAssistantService>());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        // Act
        var result = await controller.Chat(new AssistantChatRequest
        {
            Message = " "
        }, CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Chat_WithLocalizationRequest_ForwardsLanguageAndStrictMode()
    {
        var assistantService = new Mock<IAssistantService>();
        assistantService
            .Setup(service => service.ChatAsync(
                "Ola",
                "conversation-2",
                It.Is<AssistantLocalizationRequest?>(request => request != null
                    && request.ExplicitLanguage == "pt"
                    && request.StrictMode),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssistantChatResult("conversation-2", "assistant response"));

        var controller = new AssistantController(assistantService.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        controller.Request.Headers.AcceptLanguage = "pt-BR,pt;q=0.9,en;q=0.8";

        var result = await controller.Chat(new AssistantChatRequest
        {
            Message = "Ola",
            ConversationId = "conversation-2",
            Language = "pt",
            StrictMode = true
        }, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AssistantChatResponse>(okResult.Value);
        Assert.Equal("conversation-2", response.ConversationId);
        assistantService.Verify(service => service.ChatAsync(
            "Ola",
            "conversation-2",
            It.Is<AssistantLocalizationRequest?>(request => request != null && request.StrictMode && request.ExplicitLanguage == "pt"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}