using Moq;
using Domain.DotNet;
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
            .ReturnsAsync(new OllamaChatResult("", [new AssistantToolCall("retrieve_repository_context", new Dictionary<string, object?> { ["query"] = "What spicy chicken meal can I cook?", ["intent"] = "RecipeDiscovery" })]))
            .ReturnsAsync(new OllamaChatResult("Use the spicy chicken recipe.", []));
        var retrievalService = new Mock<IRetrievalService>();
        retrievalService.Setup(service => service.RetrieveAsync(It.IsAny<RetrievalRequestContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RetrievalResult(
                "What spicy chicken meal can I cook?",
            [new RetrievalRecipe("1", "1", "Spicy Chicken", "Dinner", "spicy", ["chicken"], "Slice", "45", 0.91, "canonical-multilingual-projection")],
                [new SourceAttribution("1", "Spicy Chicken", 0.91)]));
        var promptBuilder = new Mock<IPromptBuilder>();
        promptBuilder.Setup(builder => builder.Build(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<RetrievalRecipe>>(),
                It.IsAny<string?>(),
                It.IsAny<LocalizationOptions>(),
                It.IsAny<string?>()))
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
        ollamaClient.Verify(client => client.ChatAsync(null, It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<IReadOnlyList<ToolDefinition>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ChatAsync_WithMealPlanningRequest_UsesStructuredRecipeIdWorkflow()
    {
        var ollamaClient = new Mock<IOllamaClient>();
        ollamaClient
            .Setup(client => client.ChatAsync(null, It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<IReadOnlyList<ToolDefinition>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OllamaChatResult("{\"days\":[{\"day\":1,\"lunch\":{\"recipeId\":\"1\"},\"dinner\":{\"recipeId\":\"2\"}}]}", []));

        var retrievalService = new Mock<IRetrievalService>();
        retrievalService
            .Setup(service => service.RetrieveAsync(It.IsAny<RetrievalRequestContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RetrievalResult(
                "Create a weekly meal plan for me",
                [
                    new RetrievalRecipe("1", "1", "Spicy Chicken", "Dinner", "spicy", ["chicken"], "Slice", "45", 0.91, "canonical-multilingual-projection"),
                    new RetrievalRecipe("2", "2", "Tofu Bowl", "Dinner", "vegetarian", ["tofu"], "Cook", "30", 0.83, "canonical-multilingual-projection")
                ],
                [
                    new SourceAttribution("1", "Spicy Chicken", 0.91),
                    new SourceAttribution("2", "Tofu Bowl", 0.83)
                ]));

        var toolExecutor = new Mock<IToolExecutor>();
        toolExecutor.Setup(executor => executor.GetTools()).Returns([]);
        var service = CreateService(
            ollamaClient.Object,
            new InMemoryConversationStore(Options.Create(new ConversationStoreOptions())),
            toolExecutor.Object,
            retrievalService: retrievalService.Object,
            promptBuilder: Mock.Of<IPromptBuilder>());

        var result = await service.ChatAsync("Create a weekly meal plan for me");

        Assert.Contains("Validated meal plan:", result.Response);
        Assert.Contains("Lunch: Spicy Chicken (recipeId: 1)", result.Response);
        Assert.Contains("Dinner: Tofu Bowl (recipeId: 2)", result.Response);
        Assert.Contains("\"recipeId\":\"1\"", result.Response);
        Assert.Contains("\"recipeId\":\"2\"", result.Response);

        retrievalService.Verify(service => service.RetrieveAsync(It.IsAny<RetrievalRequestContext>(), It.IsAny<CancellationToken>()), Times.Once);
        ollamaClient.Verify(client => client.ChatAsync(null, It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<IReadOnlyList<ToolDefinition>>(), It.IsAny<CancellationToken>()), Times.Once);
        toolExecutor.Verify(executor => executor.ExecuteAsync(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, object?>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChatAsync_WithMealPlanningRequestAndInvalidRecipeIds_ReturnsStructuredValidationFallback()
    {
        var ollamaClient = new Mock<IOllamaClient>();
        ollamaClient
            .SetupSequence(client => client.ChatAsync(null, It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<IReadOnlyList<ToolDefinition>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OllamaChatResult("{\"days\":[{\"day\":1,\"lunch\":{\"recipeId\":\"999\"}}]}", []))
            .ReturnsAsync(new OllamaChatResult("{\"days\":[{\"day\":1,\"dinner\":{\"recipeId\":\"not-existing\"}}]}", []))
            .ReturnsAsync(new OllamaChatResult("{\"days\":[]}", []));

        var retrievalService = new Mock<IRetrievalService>();
        retrievalService
            .Setup(service => service.RetrieveAsync(It.IsAny<RetrievalRequestContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RetrievalResult(
                "Create a weekly meal plan for me",
                [new RetrievalRecipe("1", "1", "Spicy Chicken", "Dinner", "spicy", ["chicken"], "Slice", "45", 0.91, "canonical-multilingual-projection")],
                [new SourceAttribution("1", "Spicy Chicken", 0.91)]));

        var toolExecutor = new Mock<IToolExecutor>();
        toolExecutor.Setup(executor => executor.GetTools()).Returns([]);
        var service = CreateService(
            ollamaClient.Object,
            new InMemoryConversationStore(Options.Create(new ConversationStoreOptions())),
            toolExecutor.Object,
            retrievalService: retrievalService.Object,
            promptBuilder: Mock.Of<IPromptBuilder>());

        var result = await service.ChatAsync("Create a weekly meal plan for me");

        Assert.Contains("could not validate a structured meal plan", result.Response, StringComparison.OrdinalIgnoreCase);
        retrievalService.Verify(service => service.RetrieveAsync(It.IsAny<RetrievalRequestContext>(), It.IsAny<CancellationToken>()), Times.Once);
        ollamaClient.Verify(client => client.ChatAsync(null, It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<IReadOnlyList<ToolDefinition>>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task ChatAsync_WithUnsupportedRagClaims_ReturnsRetrievalBackedFallbackResponse()
    {
        var ollamaClient = new Mock<IOllamaClient>();
        ollamaClient
            .SetupSequence(client => client.ChatAsync(null, It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<IReadOnlyList<ToolDefinition>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OllamaChatResult("", [new AssistantToolCall("retrieve_repository_context", new Dictionary<string, object?> { ["query"] = "What spicy chicken meal can I cook?" })]))
            .ReturnsAsync(new OllamaChatResult("The repository does not contain enough information to answer that question.", []));
        var retrievalService = new Mock<IRetrievalService>();
        retrievalService.Setup(service => service.RetrieveAsync(It.IsAny<RetrievalRequestContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RetrievalResult(
                "What spicy chicken meal can I cook?",
            [new RetrievalRecipe("1", "1", "Spicy Chicken", "Dinner", "spicy", ["chicken"], "Slice", "45", 0.91, "canonical-multilingual-projection")],
                [new SourceAttribution("1", "Spicy Chicken", 0.91)]));
        var promptBuilder = new Mock<IPromptBuilder>();
        promptBuilder.Setup(builder => builder.Build(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<RetrievalRecipe>>(),
                It.IsAny<string?>(),
                It.IsAny<LocalizationOptions>(),
                It.IsAny<string?>()))
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

        Assert.Contains("repository does not contain enough information", result.Response, StringComparison.OrdinalIgnoreCase);
        ollamaClient.Verify(client => client.ChatAsync(null, It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<IReadOnlyList<ToolDefinition>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ChatAsync_WithSupportedRagClaims_ReturnsModelAnswer()
    {
        var ollamaClient = new Mock<IOllamaClient>();
        ollamaClient
            .SetupSequence(client => client.ChatAsync(null, It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<IReadOnlyList<ToolDefinition>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OllamaChatResult("", [new AssistantToolCall("retrieve_repository_context", new Dictionary<string, object?> { ["query"] = "What spicy chicken meal can I cook?" })]))
            .ReturnsAsync(new OllamaChatResult("Use the spicy chicken recipe.", []));
        var retrievalService = new Mock<IRetrievalService>();
        retrievalService.Setup(service => service.RetrieveAsync(It.IsAny<RetrievalRequestContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RetrievalResult(
                "What spicy chicken meal can I cook?",
            [new RetrievalRecipe("1", "1", "Spicy Chicken", "Dinner", "spicy", ["chicken"], "Slice", "45", 0.91, "canonical-multilingual-projection")],
                [new SourceAttribution("1", "Spicy Chicken", 0.91)]));
        var promptBuilder = new Mock<IPromptBuilder>();
        promptBuilder.Setup(builder => builder.Build(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<RetrievalRecipe>>(),
                It.IsAny<string?>(),
                It.IsAny<LocalizationOptions>(),
                It.IsAny<string?>()))
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
    }

    [Fact]
    public async Task ChatAsync_WithEmptyRetrieval_ReturnsNoRepositoryInformationMessage()
    {
        var ollamaClient = new Mock<IOllamaClient>();
        ollamaClient
            .SetupSequence(client => client.ChatAsync(null, It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<IReadOnlyList<ToolDefinition>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OllamaChatResult("", [new AssistantToolCall("retrieve_repository_context", new Dictionary<string, object?> { ["query"] = "What freezer meal uses moon dust?" })]))
            .ReturnsAsync(new OllamaChatResult("The repository does not contain enough information to answer that question.", []));
        var retrievalService = new Mock<IRetrievalService>();
        retrievalService.Setup(service => service.RetrieveAsync(It.IsAny<RetrievalRequestContext>(), It.IsAny<CancellationToken>()))
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
    }

    [Fact]
    public async Task ChatAsync_FollowUpReference_EnrichesRetrievalQuery_FromPreviousAssistantSources()
    {
        var ollamaClient = new Mock<IOllamaClient>();
        ollamaClient
            .SetupSequence(client => client.ChatAsync(null, It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<IReadOnlyList<ToolDefinition>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OllamaChatResult("", [new AssistantToolCall("retrieve_repository_context", new Dictionary<string, object?> { ["query"] = "Give me the full recipe you suggested." })]))
            .ReturnsAsync(new OllamaChatResult("Here is the complete recipe.", []));

        RetrievalRequestContext? capturedRequest = null;
        var retrievalService = new Mock<IRetrievalService>();
        retrievalService
            .Setup(service => service.RetrieveAsync(It.IsAny<RetrievalRequestContext>(), It.IsAny<CancellationToken>()))
            .Callback<RetrievalRequestContext, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new RetrievalResult(
                "Give me the full recipe you suggested.",
                [new RetrievalRecipe("frango-salsa-verde", "frango-salsa-verde", "Frango Salsa Verde", "Dinner", "quick", ["chicken"], "prep", "45", 0.91, "canonical-multilingual-projection")],
                [new SourceAttribution("frango-salsa-verde", "Frango Salsa Verde", 0.91)]));

        var promptBuilder = new Mock<IPromptBuilder>();
        promptBuilder
            .Setup(builder => builder.Build(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<RetrievalRecipe>>(),
                It.IsAny<string?>(),
                It.IsAny<LocalizationOptions>(),
                It.IsAny<string?>()))
            .Returns("rag prompt");

        var conversationStore = new InMemoryConversationStore(Options.Create(new ConversationStoreOptions()));
        conversationStore.AppendMessages("conversation-1", [
            new ConversationMessage(ConversationRole.User, "Suggest me a chicken meal.", DateTimeOffset.UtcNow),
            new ConversationMessage(ConversationRole.Assistant, "Use Frango Salsa Verde.\n\nSources:\n- frango-salsa-verde: Frango Salsa Verde", DateTimeOffset.UtcNow)
        ]);

        var toolExecutor = new Mock<IToolExecutor>();
        toolExecutor.Setup(executor => executor.GetTools()).Returns([]);
        var service = CreateService(
            ollamaClient.Object,
            conversationStore,
            toolExecutor.Object,
            retrievalService: retrievalService.Object,
            promptBuilder: promptBuilder.Object);

        await service.ChatAsync("Give me the full recipe you suggested.", "conversation-1");

        Assert.NotNull(capturedRequest);
        Assert.Contains("Give me the full recipe you suggested.", capturedRequest!.OriginalQuestion);
        Assert.Contains("Frango Salsa Verde", capturedRequest.OriginalQuestion);
        Assert.Contains("frango-salsa-verde", capturedRequest.OriginalQuestion);
    }

    [Fact]
    public async Task ChatAsync_PortugueseFollowUpReference_EnrichesRetrievalQuery_FromPreviousAssistantSources()
    {
        var ollamaClient = new Mock<IOllamaClient>();
        ollamaClient
            .SetupSequence(client => client.ChatAsync(null, It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<IReadOnlyList<ToolDefinition>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OllamaChatResult("", [new AssistantToolCall("retrieve_repository_context", new Dictionary<string, object?> { ["query"] = "Dá-me a receita que sugeriste." })]))
            .ReturnsAsync(new OllamaChatResult("Aqui tens a receita completa.", []));

        RetrievalRequestContext? capturedRequest = null;
        var retrievalService = new Mock<IRetrievalService>();
        retrievalService
            .Setup(service => service.RetrieveAsync(It.IsAny<RetrievalRequestContext>(), It.IsAny<CancellationToken>()))
            .Callback<RetrievalRequestContext, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new RetrievalResult(
                "Dá-me a receita que sugeriste.",
                [new RetrievalRecipe("tofu-chorizo", "tofu-chorizo", "Tofu Chorizo", "Jantar", "rapido", ["tofu"], "prep", "30", 0.88, "per-language-projection")],
                [new SourceAttribution("tofu-chorizo", "Tofu Chorizo", 0.88)]));

        var promptBuilder = new Mock<IPromptBuilder>();
        promptBuilder
            .Setup(builder => builder.Build(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<RetrievalRecipe>>(),
                It.IsAny<string?>(),
                It.IsAny<LocalizationOptions>(),
                It.IsAny<string?>()))
            .Returns("rag prompt");

        var conversationStore = new InMemoryConversationStore(Options.Create(new ConversationStoreOptions()));
        conversationStore.AppendMessages("conversation-pt", [
            new ConversationMessage(ConversationRole.User, "Que receita vegetariana recomendas?", DateTimeOffset.UtcNow),
            new ConversationMessage(ConversationRole.Assistant, "Sugiro Tofu Chorizo.\n\nSources:\n- tofu-chorizo: Tofu Chorizo", DateTimeOffset.UtcNow)
        ]);

        var toolExecutor = new Mock<IToolExecutor>();
        toolExecutor.Setup(executor => executor.GetTools()).Returns([]);
        var service = CreateService(
            ollamaClient.Object,
            conversationStore,
            toolExecutor.Object,
            retrievalService: retrievalService.Object,
            promptBuilder: promptBuilder.Object);

        await service.ChatAsync("Dá-me a receita que sugeriste.", "conversation-pt");

        Assert.NotNull(capturedRequest);
        Assert.Contains("Dá-me a receita que sugeriste.", capturedRequest!.OriginalQuestion);
        Assert.Contains("Receita referida:", capturedRequest.OriginalQuestion);
        Assert.Contains("Tofu Chorizo", capturedRequest.OriginalQuestion);
        Assert.Contains("tofu-chorizo", capturedRequest.OriginalQuestion);
    }

    [Fact]
    public async Task ChatAsync_UsesDetectedLanguage_WhenExplicitLanguageMissing()
    {
        var conversationStore = new InMemoryConversationStore(Options.Create(new ConversationStoreOptions()));
        var orchestrator = new Mock<IAssistantOrchestrator>();
        orchestrator
            .Setup(candidate => candidate.ExecuteAsync(It.IsAny<OrchestratorContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrchestratorResult("assistant response", "agent", [], [], [], TimeSpan.Zero, [], []));

        var languageContextResolver = new Mock<ILanguageContextResolver>();
        languageContextResolver
            .Setup(resolver => resolver.Resolve(It.IsAny<string?>(), It.IsAny<IEnumerable<string>?>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string?>()))
            .Returns(new LanguageContext(null, Array.Empty<string>(), "en", false, "pt"));

        var localizationOptionsFactory = new Mock<ILocalizationOptionsFactory>();
        localizationOptionsFactory
            .Setup(factory => factory.Create(It.IsAny<LanguageContext>()))
            .Returns(LocalizationOptions.Create("pt", ["en"]));

        var service = new AssistantService(
            conversationStore,
            orchestrator.Object,
            languageContextResolver.Object,
            localizationOptionsFactory.Object,
            Options.Create(new AssistantOptions()),
            Options.Create(new AssistantLocalizationDefaultsOptions { DefaultLanguage = "en", SupportedLanguages = ["en", "pt"] }),
            NullLogger<AssistantService>.Instance);

        await service.ChatAsync("Que receitas tens com frango?");

        languageContextResolver.Verify(resolver => resolver.Resolve(
            null,
            It.IsAny<IEnumerable<string>?>(),
            "en",
            false,
            "pt"), Times.Once);
    }

    [Fact]
    public async Task ChatAsync_PropagatesDetectedLanguageAndResolvedLocalization_ToPromptBuilder()
    {
        var ollamaClient = new Mock<IOllamaClient>();
        ollamaClient
            .SetupSequence(client => client.ChatAsync(null, It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<IReadOnlyList<ToolDefinition>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OllamaChatResult("", [new AssistantToolCall("retrieve_repository_context", new Dictionary<string, object?> { ["query"] = "Que receitas tens com frango?", ["intent"] = "RecipeDiscovery" })]))
            .ReturnsAsync(new OllamaChatResult("Use Frango Salsa Verde.", []));

        var retrievalService = new Mock<IRetrievalService>();
        retrievalService.Setup(service => service.RetrieveAsync(It.IsAny<RetrievalRequestContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RetrievalResult(
                "Que receitas tens com frango?",
                [new RetrievalRecipe("1", "1", "Frango Salsa Verde", "Jantar", "frango", ["frango"], "Assar", "45", 0.91, "per-language-projection")],
                [new SourceAttribution("1", "Frango Salsa Verde", 0.91)]));

        LocalizationOptions? capturedLocalization = null;
        string? capturedRequestedLanguage = null;
        var promptBuilder = new Mock<IPromptBuilder>();
        promptBuilder
            .Setup(builder => builder.Build(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<RetrievalRecipe>>(),
                It.IsAny<string?>(),
                It.IsAny<LocalizationOptions>(),
                It.IsAny<string?>()))
            .Callback<string, IReadOnlyList<RetrievalRecipe>, string?, LocalizationOptions, string?>((_, _, _, localization, requestedLanguage) =>
            {
                capturedLocalization = localization;
                capturedRequestedLanguage = requestedLanguage;
            })
            .Returns("rag prompt");

        var toolExecutor = new Mock<IToolExecutor>();
        toolExecutor.Setup(executor => executor.GetTools()).Returns([]);
        var service = CreateService(
            ollamaClient.Object,
            new InMemoryConversationStore(Options.Create(new ConversationStoreOptions())),
            toolExecutor.Object,
            retrievalService: retrievalService.Object,
            promptBuilder: promptBuilder.Object,
            localizationDefaults: new AssistantLocalizationDefaultsOptions { DefaultLanguage = "en", SupportedLanguages = ["en", "pt"] });

        await service.ChatAsync("Que receitas tens com frango?");

        Assert.NotNull(capturedLocalization);
        Assert.Equal("pt", capturedLocalization!.PreferredLanguage);
        Assert.Equal(new[] { "en" }, capturedLocalization.FallbackLanguages);
        Assert.False(capturedLocalization.StrictMode);
        Assert.Equal("pt", capturedRequestedLanguage);
    }

    [Fact]
    public async Task ChatAsync_ExplicitLanguage_OverridesDetectedLanguage()
    {
        var conversationStore = new InMemoryConversationStore(Options.Create(new ConversationStoreOptions()));
        var orchestrator = new Mock<IAssistantOrchestrator>();
        orchestrator
            .Setup(candidate => candidate.ExecuteAsync(It.IsAny<OrchestratorContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrchestratorResult("assistant response", "agent", [], [], [], TimeSpan.Zero, [], []));

        var languageContextResolver = new Mock<ILanguageContextResolver>();
        languageContextResolver
            .Setup(resolver => resolver.Resolve(It.IsAny<string?>(), It.IsAny<IEnumerable<string>?>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string?>()))
            .Returns(new LanguageContext("en", Array.Empty<string>(), "en", false));

        var localizationOptionsFactory = new Mock<ILocalizationOptionsFactory>();
        localizationOptionsFactory
            .Setup(factory => factory.Create(It.IsAny<LanguageContext>()))
            .Returns(LocalizationOptions.Create("en"));

        var service = new AssistantService(
            conversationStore,
            orchestrator.Object,
            languageContextResolver.Object,
            localizationOptionsFactory.Object,
            Options.Create(new AssistantOptions()),
            Options.Create(new AssistantLocalizationDefaultsOptions { DefaultLanguage = "en", SupportedLanguages = ["en", "pt"] }),
            NullLogger<AssistantService>.Instance);

        await service.ChatAsync(
            "Que receitas tens com frango?",
            localization: new AssistantLocalizationRequest("en", Array.Empty<string>(), false));

        languageContextResolver.Verify(resolver => resolver.Resolve(
            "en",
            It.IsAny<IEnumerable<string>?>(),
            "en",
            false,
            null), Times.Once);
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
            Mock.Of<ILanguageContextResolver>(),
            Mock.Of<ILocalizationOptionsFactory>(),
            Options.Create(new AssistantOptions()),
            Options.Create(new AssistantLocalizationDefaultsOptions()),
            NullLogger<AssistantService>.Instance));
    }

    private static AssistantService CreateService(
        IOllamaClient ollamaClient,
        IConversationStore conversationStore,
        IToolExecutor toolExecutor,
        AssistantOptions? options = null,
        IRetrievalService? retrievalService = null,
        IPromptBuilder? promptBuilder = null,
        IAnswerGroundingService? answerGroundingService = null,
        AssistantLocalizationDefaultsOptions? localizationDefaults = null,
        IAssistantLanguageDetector? languageDetector = null)
    {
        var agent = new MealPlanningAgent(
            ollamaClient,
            toolExecutor,
            new DefaultRoutingPolicy(),
            NullLogger<MealPlanningAgent>.Instance,
            retrievalService,
            promptBuilder);
        var orchestrator = new AssistantOrchestrator([agent], new DefaultRoutingPolicy(), NullLogger<AssistantOrchestrator>.Instance);
        return new AssistantService(
            conversationStore,
            orchestrator,
            new LanguageContextResolver(),
            new LocalizationOptionsFactory(),
            Options.Create(options ?? new AssistantOptions()),
                Options.Create(localizationDefaults ?? new AssistantLocalizationDefaultsOptions { DefaultLanguage = "en", SupportedLanguages = ["en", "pt", "es", "de", "fr"] }),
                NullLogger<AssistantService>.Instance,
                languageDetector);
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