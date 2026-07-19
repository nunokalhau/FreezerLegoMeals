using Moq;
using Microsoft.Extensions.Options;
using Xunit;

namespace Services.DotNet.UnitTests;

public class AssistantServiceTests
{
    [Fact]
    public async Task ChatAsync_CreatesConversationAndPersistsMessages()
    {
        // Arrange
        var ollamaClient = new Mock<IOllamaClient>();
        IReadOnlyList<ConversationMessage>? capturedMessages = null;
        ollamaClient
            .Setup(client => client.ChatAsync(null, It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<CancellationToken>()))
            .Callback<string?, IReadOnlyList<ConversationMessage>, CancellationToken>((_, messages, _) => capturedMessages = messages)
            .ReturnsAsync("assistant response");
        var conversationStore = new InMemoryConversationStore(Options.Create(new ConversationStoreOptions()));
        var service = new AssistantService(ollamaClient.Object, conversationStore, Options.Create(new AssistantOptions
        {
            SystemPrompt = "system prompt"
        }));

        // Act
        var result = await service.ChatAsync("Hello", cancellationToken: CancellationToken.None);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(result.ConversationId));
        Assert.Equal("assistant response", result.Response);
        Assert.NotNull(capturedMessages);
        Assert.Equal(2, capturedMessages.Count);
        Assert.Equal(ConversationRole.System, capturedMessages[0].Role);
        Assert.Equal("system prompt", capturedMessages[0].Content);
        Assert.Equal(ConversationRole.User, capturedMessages[1].Role);
        Assert.Equal("Hello", capturedMessages[1].Content);

        var conversation = conversationStore.GetOrCreateConversation(result.ConversationId);
        Assert.Equal(2, conversation.Messages.Count);
        Assert.Equal(ConversationRole.User, conversation.Messages[0].Role);
        Assert.Equal(ConversationRole.Assistant, conversation.Messages[1].Role);
    }

    [Fact]
    public async Task ChatAsync_WithExistingConversationIncludesHistory()
    {
        // Arrange
        var ollamaClient = new Mock<IOllamaClient>();
        IReadOnlyList<ConversationMessage>? capturedMessages = null;
        ollamaClient
            .Setup(client => client.ChatAsync(null, It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<CancellationToken>()))
            .Callback<string?, IReadOnlyList<ConversationMessage>, CancellationToken>((_, messages, _) => capturedMessages = messages)
            .ReturnsAsync("second response");
        var conversationStore = new InMemoryConversationStore(Options.Create(new ConversationStoreOptions()));
        conversationStore.AppendMessages("conversation-1", [
            new ConversationMessage(ConversationRole.User, "First", DateTimeOffset.UtcNow),
            new ConversationMessage(ConversationRole.Assistant, "First response", DateTimeOffset.UtcNow)
        ]);
        var service = new AssistantService(ollamaClient.Object, conversationStore, Options.Create(new AssistantOptions()));

        // Act
        var result = await service.ChatAsync("Second", "conversation-1");

        // Assert
        Assert.Equal("conversation-1", result.ConversationId);
        Assert.NotNull(capturedMessages);
        Assert.Equal(4, capturedMessages.Count);
        Assert.Equal(ConversationRole.System, capturedMessages[0].Role);
        Assert.Equal("First", capturedMessages[1].Content);
        Assert.Equal("First response", capturedMessages[2].Content);
        Assert.Equal("Second", capturedMessages[3].Content);
    }

    [Fact]
    public void Constructor_WithNullOllamaClientThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new AssistantService(
            null!,
            Mock.Of<IConversationStore>(),
            Options.Create(new AssistantOptions())));
    }
}