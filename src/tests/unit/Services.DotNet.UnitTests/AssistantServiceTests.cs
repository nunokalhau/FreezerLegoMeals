using Moq;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Orchestration.DotNet;
using RAG.DotNet;
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
            .Setup(client => client.ChatAsync(null, It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<IReadOnlyList<ToolDefinition>>(), It.IsAny<CancellationToken>()))
            .Callback<string?, IReadOnlyList<ConversationMessage>, IReadOnlyList<ToolDefinition>, CancellationToken>((_, messages, _, _) => capturedMessages = messages)
            .ReturnsAsync(new OllamaChatResult("assistant response", []));
        var conversationStore = new InMemoryConversationStore(Options.Create(new ConversationStoreOptions()));
        var toolExecutor = new Mock<IToolExecutor>();
        toolExecutor.Setup(executor => executor.GetTools()).Returns([]);
        var service = CreateService(ollamaClient.Object, conversationStore, toolExecutor.Object, new AssistantOptions
        {
            SystemPrompt = "system prompt"
        });

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
            .Setup(client => client.ChatAsync(null, It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<IReadOnlyList<ToolDefinition>>(), It.IsAny<CancellationToken>()))
            .Callback<string?, IReadOnlyList<ConversationMessage>, IReadOnlyList<ToolDefinition>, CancellationToken>((_, messages, _, _) => capturedMessages = messages)
            .ReturnsAsync(new OllamaChatResult("second response", []));
        var conversationStore = new InMemoryConversationStore(Options.Create(new ConversationStoreOptions()));
        conversationStore.AppendMessages("conversation-1", [
            new ConversationMessage(ConversationRole.User, "First", DateTimeOffset.UtcNow),
            new ConversationMessage(ConversationRole.Assistant, "First response", DateTimeOffset.UtcNow)
        ]);
        var toolExecutor = new Mock<IToolExecutor>();
        toolExecutor.Setup(executor => executor.GetTools()).Returns([]);
        var service = CreateService(ollamaClient.Object, conversationStore, toolExecutor.Object);

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
    public async Task ChatAsync_WithOneToolCall_ExecutesToolAndReturnsFinalResponse()
    {
        var ollamaClient = new Mock<IOllamaClient>();
        ollamaClient
            .SetupSequence(client => client.ChatAsync(null, It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<IReadOnlyList<ToolDefinition>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OllamaChatResult("", [new AssistantToolCall("example_tool", new Dictionary<string, object?> { ["message"] = "hello" })]))
            .ReturnsAsync(new OllamaChatResult("done", []));
        var toolExecutor = CreateToolExecutor();
        var conversationStore = new InMemoryConversationStore(Options.Create(new ConversationStoreOptions()));
        var service = CreateService(ollamaClient.Object, conversationStore, toolExecutor.Object);

        var result = await service.ChatAsync("Use a tool");

        Assert.Equal("done", result.Response);
        toolExecutor.Verify(executor => executor.ExecuteAsync("example_tool", It.Is<IReadOnlyDictionary<string, object?>>(args => (string?)args["message"] == "hello"), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains(conversationStore.GetOrCreateConversation(result.ConversationId).Messages, message => message.Role == ConversationRole.Tool);
    }

    [Fact]
    public async Task ChatAsync_WithRepositoryQuestion_UsesRagAndIncludesSources()
    {
        var ollamaClient = new Mock<IOllamaClient>();
        ollamaClient
            .SetupSequence(client => client.ChatAsync(null, It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<IReadOnlyList<ToolDefinition>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OllamaChatResult("direct draft", []))
            .ReturnsAsync(new OllamaChatResult("Use the spicy chicken recipe.", []));
        var retrievalService = new Mock<IRetrievalService>();
        retrievalService.Setup(service => service.RetrieveAsync("What spicy chicken meal can I cook?", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RetrievalResult(
                "What spicy chicken meal can I cook?",
                [new RetrievalRecipe("1", "Spicy Chicken", "Dinner", "spicy", ["chicken"], "Slice", "45", 0.91)],
                [new SourceAttribution("1", "Spicy Chicken", 0.91)]));
        var promptBuilder = new Mock<IPromptBuilder>();
        promptBuilder.Setup(builder => builder.Build(It.IsAny<string>(), It.IsAny<IReadOnlyList<RetrievalRecipe>>()))
            .Returns("rag prompt");
        var toolExecutor = new Mock<IToolExecutor>();
        toolExecutor.Setup(executor => executor.GetTools()).Returns([]);
        var service = CreateService(
            ollamaClient.Object,
            new InMemoryConversationStore(Options.Create(new ConversationStoreOptions())),
            toolExecutor.Object,
            retrievalService: retrievalService.Object,
            promptBuilder: promptBuilder.Object);

        var result = await service.ChatAsync("What spicy chicken meal can I cook?");

        Assert.Contains("Use the spicy chicken recipe.", result.Response);
        Assert.Contains("Sources:", result.Response);
        Assert.Contains("1: Spicy Chicken", result.Response);
        ollamaClient.Verify(client => client.ChatAsync(null, It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<IReadOnlyList<ToolDefinition>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ChatAsync_WithEmptyRetrieval_ReturnsNoRepositoryInformationMessage()
    {
        var ollamaClient = new Mock<IOllamaClient>();
        ollamaClient.Setup(client => client.ChatAsync(null, It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<IReadOnlyList<ToolDefinition>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OllamaChatResult("direct draft", []));
        var retrievalService = new Mock<IRetrievalService>();
        retrievalService.Setup(service => service.RetrieveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RetrievalResult("unknown", [], []));
        var toolExecutor = new Mock<IToolExecutor>();
        toolExecutor.Setup(executor => executor.GetTools()).Returns([]);
        var service = CreateService(
            ollamaClient.Object,
            new InMemoryConversationStore(Options.Create(new ConversationStoreOptions())),
            toolExecutor.Object,
            retrievalService: retrievalService.Object,
            promptBuilder: Mock.Of<IPromptBuilder>());

        var result = await service.ChatAsync("What freezer meal uses moon dust?");

        Assert.Contains("repository does not contain enough information", result.Response);
        Assert.Contains("Sources: none", result.Response);
    }

    [Fact]
    public async Task ChatAsync_WithMultipleSequentialToolCalls_ExecutesEachTool()
    {
        var ollamaClient = new Mock<IOllamaClient>();
        ollamaClient
            .SetupSequence(client => client.ChatAsync(null, It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<IReadOnlyList<ToolDefinition>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OllamaChatResult("", [new AssistantToolCall("example_tool", new Dictionary<string, object?>())]))
            .ReturnsAsync(new OllamaChatResult("", [new AssistantToolCall("second_tool", new Dictionary<string, object?>())]))
            .ReturnsAsync(new OllamaChatResult("complete", []));
        var toolExecutor = CreateToolExecutor();
        var service = CreateService(ollamaClient.Object, new InMemoryConversationStore(Options.Create(new ConversationStoreOptions())), toolExecutor.Object);

        var result = await service.ChatAsync("Use tools");

        Assert.Equal("complete", result.Response);
        toolExecutor.Verify(executor => executor.ExecuteAsync(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, object?>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ChatAsync_WithToolFailure_AppendsFailureAndReturnsFinalResponse()
    {
        var ollamaClient = new Mock<IOllamaClient>();
        ollamaClient
            .SetupSequence(client => client.ChatAsync(null, It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<IReadOnlyList<ToolDefinition>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OllamaChatResult("", [new AssistantToolCall("example_tool", new Dictionary<string, object?>())]))
            .ReturnsAsync(new OllamaChatResult("could not use tool", []));
        var toolExecutor = CreateToolExecutor(success: false);
        var conversationStore = new InMemoryConversationStore(Options.Create(new ConversationStoreOptions()));
        var service = CreateService(ollamaClient.Object, conversationStore, toolExecutor.Object);

        var result = await service.ChatAsync("Use failing tool");

        Assert.Equal("could not use tool", result.Response);
        Assert.Contains(conversationStore.GetOrCreateConversation(result.ConversationId).Messages, message => message.Role == ConversationRole.Tool && message.Content.Contains("failed"));
    }

    [Fact]
    public async Task ChatAsync_WithInvalidTool_AppendsFailureAndReturnsFinalResponse()
    {
        var ollamaClient = new Mock<IOllamaClient>();
        ollamaClient
            .SetupSequence(client => client.ChatAsync(null, It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<IReadOnlyList<ToolDefinition>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OllamaChatResult("", [new AssistantToolCall("missing_tool", new Dictionary<string, object?>())]))
            .ReturnsAsync(new OllamaChatResult("invalid tool handled", []));
        var toolExecutor = CreateToolExecutor();
        toolExecutor.Setup(executor => executor.ExecuteAsync("missing_tool", It.IsAny<IReadOnlyDictionary<string, object?>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Unknown tool: missing_tool"));
        var service = CreateService(ollamaClient.Object, new InMemoryConversationStore(Options.Create(new ConversationStoreOptions())), toolExecutor.Object);

        var result = await service.ChatAsync("Use missing tool");

        Assert.Equal("invalid tool handled", result.Response);
    }

    [Fact]
    public async Task ChatAsync_WhenToolCallLimitExceeded_ReturnsGracefulError()
    {
        var ollamaClient = new Mock<IOllamaClient>();
        ollamaClient
            .Setup(client => client.ChatAsync(null, It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<IReadOnlyList<ToolDefinition>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OllamaChatResult("", [
                new AssistantToolCall("example_tool", new Dictionary<string, object?>()),
                new AssistantToolCall("second_tool", new Dictionary<string, object?>())
            ]));
        var toolExecutor = CreateToolExecutor();
        var service = CreateService(
            ollamaClient.Object,
            new InMemoryConversationStore(Options.Create(new ConversationStoreOptions())),
            toolExecutor.Object,
            new AssistantOptions { MaximumToolCallsPerRequest = 1 });

        var result = await service.ChatAsync("Loop forever");

        Assert.Contains("maximum tool call limit", result.Response);
        toolExecutor.Verify(executor => executor.ExecuteAsync(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, object?>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Constructor_WithNullOrchestratorThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new AssistantService(
            Mock.Of<IConversationStore>(),
            null!,
            Options.Create(new AssistantOptions()),
            NullLogger<AssistantService>.Instance));
    }

    private static AssistantService CreateService(
        IOllamaClient ollamaClient,
        IConversationStore conversationStore,
        IToolExecutor toolExecutor,
        AssistantOptions? options = null,
        IRetrievalService? retrievalService = null,
        IPromptBuilder? promptBuilder = null)
    {
        var agent = new MealPlanningAgent(
            ollamaClient,
            toolExecutor,
            NullLogger<MealPlanningAgent>.Instance,
            retrievalService,
            promptBuilder);
        var orchestrator = new Orchestrator([agent], NullLogger<Orchestrator>.Instance);
        return new AssistantService(
            conversationStore,
            orchestrator,
            Options.Create(options ?? new AssistantOptions()),
            NullLogger<AssistantService>.Instance);
    }

    private static Mock<IToolExecutor> CreateToolExecutor(bool success = true)
    {
        var toolExecutor = new Mock<IToolExecutor>();
        toolExecutor.Setup(executor => executor.GetTools()).Returns([
            new ToolDefinition { Name = "example_tool", Description = "Example tool" },
            new ToolDefinition { Name = "second_tool", Description = "Second tool" }
        ]);
        toolExecutor.Setup(executor => executor.ExecuteAsync(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, object?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string tool, IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken) => new ToolExecutionResult
            {
                Success = success,
                Tool = tool,
                Output = success ? new { ok = true } : null,
                Error = success ? null : "failed"
            });

        return toolExecutor;
    }
}