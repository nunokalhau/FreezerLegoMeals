using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Orchestration.DotNet;
using Services.DotNet;
using Xunit;

namespace Services.DotNet.UnitTests;

public sealed class HybridIntentClassifierTests
{
    [Fact]
    public async Task ClassifyAsync_HighConfidenceRule_DoesNotInvokeLlm()
    {
        var ollama = new Mock<IOllamaClient>(MockBehavior.Strict);
        var classifier = CreateClassifier(ollama.Object, lowConfidenceThreshold: 0.6);

        var result = await classifier.ClassifyAsync("What recipes do you have?");

        Assert.Equal(IntentType.RecipeDiscovery, result.Intent);
        Assert.Equal("recipe-discovery", result.MatchedRule);
        ollama.Verify(client => client.ChatAsync(It.IsAny<string?>(), It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<IReadOnlyList<ToolDefinition>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ClassifyAsync_LowConfidenceRule_InvokesLlmAndUsesLlmResult()
    {
        var ollama = new Mock<IOllamaClient>();
        ollama
            .Setup(client => client.ChatAsync(It.IsAny<string?>(), It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<IReadOnlyList<ToolDefinition>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OllamaChatResult("{\"intent\":\"MealPlanning\",\"confidence\":0.91,\"language\":\"en\"}", []));

        var classifier = CreateClassifier(ollama.Object, lowConfidenceThreshold: 0.7);

        var result = await classifier.ClassifyAsync("hello there");

        Assert.Equal(IntentType.MealPlanning, result.Intent);
        Assert.Equal(0.91, result.Confidence);
        Assert.Equal("hybrid-llm", result.MatchedRule);
        ollama.Verify(client => client.ChatAsync(It.IsAny<string?>(), It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<IReadOnlyList<ToolDefinition>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ClassifyAsync_LowConfidenceRule_WhenLlmOutputInvalid_FallsBackToRuleBased()
    {
        var ollama = new Mock<IOllamaClient>();
        ollama
            .Setup(client => client.ChatAsync(It.IsAny<string?>(), It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<IReadOnlyList<ToolDefinition>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OllamaChatResult("not-json", []));

        var classifier = CreateClassifier(ollama.Object, lowConfidenceThreshold: 0.7);

        var result = await classifier.ClassifyAsync("hello there");

        Assert.Equal(IntentType.GeneralConversation, result.Intent);
        Assert.Equal("fallback", result.MatchedRule);
        Assert.Equal(0.6, result.Confidence);
    }

    private static HybridIntentClassifier CreateClassifier(IOllamaClient ollamaClient, double lowConfidenceThreshold)
    {
        return new HybridIntentClassifier(
            new RuleBasedIntentClassifier(),
            ollamaClient,
            Options.Create(new HybridIntentClassifierOptions { LowConfidenceThreshold = lowConfidenceThreshold }),
            NullLogger<HybridIntentClassifier>.Instance);
    }
}
