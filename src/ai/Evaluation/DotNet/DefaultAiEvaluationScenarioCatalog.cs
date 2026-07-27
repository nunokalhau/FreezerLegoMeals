using Orchestration.DotNet;
using Services.DotNet;

namespace Evaluation.DotNet;

public sealed class DefaultAiEvaluationScenarioCatalog : IAiEvaluationScenarioCatalog
{
    public IReadOnlyList<AiEvaluationScenario> GetDefaultScenarios()
    {
        return
        [
            CreateToolRoutingScenario(),
            CreateGroundedRetrievalScenario(),
            CreateMemoryScenario()
        ];
    }

    private static AiEvaluationScenario CreateToolRoutingScenario()
    {
        return new AiEvaluationScenario
        {
            Id = "tool-routing-and-execution",
            Description = "Validates tool-first routing, tool selection, and tool execution.",
            UserMessages = ["Call the planning tool now."],
            MockedLlmResponses =
            [
                new OllamaChatResult(
                    string.Empty,
                    [
                        new AssistantToolCall("search_recipes", new Dictionary<string, object?>
                        {
                            ["query"] = "spicy chicken"
                        })
                    ]),
                new OllamaChatResult("I found recipes and updated your weekly plan.", [])
            ],
            MockedToolResults = new Dictionary<string, ToolExecutionResult>(StringComparer.Ordinal)
            {
                ["search_recipes"] = new ToolExecutionResult
                {
                    Success = true,
                    Tool = "search_recipes",
                    Output = new { recipes = new[] { "Spicy Chicken Bowl" } }
                }
            },
            Expectations = new AiEvaluationExpectations
            {
                ExpectedRoute = AssistantRoute.InvokeTools,
                ExpectedToolSelection = ["search_recipes"],
                ExpectSuccessfulToolExecution = true,
                ExpectGroundedResponse = false,
                RequiredResponseFragments = ["recipes", "plan"],
                ExpectNoErrors = true
            }
        };
    }

    private static AiEvaluationScenario CreateGroundedRetrievalScenario()
    {
        return new AiEvaluationScenario
        {
            Id = "grounded-rag-retrieval-quality",
            Description = "Validates retrieval relevance, semantic ranking quality, grounded response generation, and RAG routing.",
            UserMessages = ["What spicy chicken meal can I cook this week?"],
            SemanticProbeQuery = "spicy chicken meal",
            MockedLlmResponses =
            [
                new OllamaChatResult("Draft answer before retrieval.", []),
                new OllamaChatResult("Cook the Spicy Chicken Bowl and freeze portions for later meals.", [])
            ],
            Expectations = new AiEvaluationExpectations
            {
                ExpectedRoute = AssistantRoute.UseRag,
                ExpectedTopSemanticRecipeId = "1",
                ExpectedRetrievedRecipeId = "1",
                ExpectGroundedResponse = true,
                RequiredResponseFragments = ["Spicy Chicken", "Sources:", "1:"],
                ExpectNoErrors = true
            }
        };
    }

    private static AiEvaluationScenario CreateMemoryScenario()
    {
        return new AiEvaluationScenario
        {
            Id = "memory-retrieval-and-direct-answer",
            Description = "Validates conversation memory retrieval and direct-answer path.",
            UserMessages = ["Hello assistant", "What did I just say?"],
            MockedLlmResponses =
            [
                new OllamaChatResult("Hello! I can help with your meal plan.", []),
                new OllamaChatResult("You said: Hello assistant.", [])
            ],
            Expectations = new AiEvaluationExpectations
            {
                ExpectedRoute = AssistantRoute.DirectAnswer,
                ExpectMemoryRecall = true,
                MinimumSecondTurnMessageCount = 4,
                RequiredPriorUserMessageInSecondTurn = "Hello assistant",
                ExpectGroundedResponse = false,
                RequiredResponseFragments = ["You said", "Hello assistant"],
                ExpectNoErrors = true
            }
        };
    }
}
