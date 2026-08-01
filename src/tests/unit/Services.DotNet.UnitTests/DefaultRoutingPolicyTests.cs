using Microsoft.Extensions.Options;
using Domain.DotNet;
using Orchestration.DotNet;
using Services.DotNet;
using Xunit;

namespace Services.DotNet.UnitTests;

public class DefaultRoutingPolicyTests
{
    [Fact]
    public void DetermineDelegatedAgent_ReturnsNull_ToPreserveCurrentBehavior()
    {
        var policy = new DefaultRoutingPolicy();
        var context = CreateContext("Plan dinners this week");

        var delegatedAgent = policy.DetermineDelegatedAgent(context, ["MealPlanningAgent", "ShoppingAgent"]);

        Assert.Null(delegatedAgent);
    }

    [Fact]
    public void DetermineAssistantRoute_WhenToolCallsPresent_ReturnsInvokeTools()
    {
        var policy = new DefaultRoutingPolicy();
        var context = CreateContext("Use a tool");
        var result = new OllamaChatResult("", [new AssistantToolCall("example_tool", new Dictionary<string, object?>())]);

        var route = policy.DetermineAssistantRoute(context, result, retrievalAvailable: true);

        Assert.Equal(AssistantRoute.InvokeTools, route);
    }

    [Fact]
    public void DetermineAssistantRoute_WhenKnowledgeQuestionAndRetrievalAvailable_ReturnsUseRag()
    {
        var policy = new DefaultRoutingPolicy();
        var context = CreateContext("What meal should I cook with chicken?");
        var result = new OllamaChatResult("draft", []);

        var route = policy.DetermineAssistantRoute(context, result, retrievalAvailable: true);

        Assert.Equal(AssistantRoute.UseRag, route);
    }

    [Fact]
    public void DetermineAssistantRoute_WhenPortugueseKnowledgeQuestionAndRetrievalAvailable_ReturnsUseRag()
    {
        var policy = new DefaultRoutingPolicy();
        var context = CreateContext("Que receitas tens com frango?");
        var result = new OllamaChatResult("draft", []);

        var route = policy.DetermineAssistantRoute(context, result, retrievalAvailable: true);

        Assert.Equal(AssistantRoute.UseRag, route);
    }

    [Fact]
    public void DetermineAssistantRoute_WhenKnowledgeQuestionAndRetrievalUnavailable_ReturnsDirectAnswer()
    {
        var policy = new DefaultRoutingPolicy();
        var context = CreateContext("What meal should I cook with chicken?");
        var result = new OllamaChatResult("draft", []);

        var route = policy.DetermineAssistantRoute(context, result, retrievalAvailable: false);

        Assert.Equal(AssistantRoute.DirectAnswer, route);
    }

    [Fact]
    public void DetermineAssistantRoute_WhenGeneralPromptWithoutTools_ReturnsDirectAnswer()
    {
        var policy = new DefaultRoutingPolicy();
        var context = CreateContext("Tell me a joke");
        var result = new OllamaChatResult("draft", []);

        var route = policy.DetermineAssistantRoute(context, result, retrievalAvailable: true);

        Assert.Equal(AssistantRoute.DirectAnswer, route);
    }

    private static OrchestratorContext CreateContext(string userRequest)
    {
        return new OrchestratorContext(
            userRequest,
            DateTimeOffset.UtcNow,
            "correlation-1",
            new Dictionary<string, object?>(),
            new LanguageContext(null, [], "en", false),
            LocalizationOptions.Create("en"),
            "conversation-1",
            [],
            [],
            Options.Create(new AssistantOptions()).Value);
    }
}
