using Orchestration.DotNet;

namespace Evaluation.DotNet;

public interface IAiEvaluationDimension
{
    string Name { get; }

    AiEvaluationDimensionResult Evaluate(AiEvaluationContext context);
}

public sealed class RoutingDecisionEvaluationDimension : IAiEvaluationDimension
{
    public string Name => "routing";

    public AiEvaluationDimensionResult Evaluate(AiEvaluationContext context)
    {
        var expectedRoute = context.Scenario.Expectations.ExpectedRoute;
        if (expectedRoute is null)
            return new AiEvaluationDimensionResult(Name, AiEvaluationStatus.NotApplicable, "Scenario did not define an expected route.");

        var actualRoute = InferRoute(context.Execution.LastOrchestratorResult);
        var passed = actualRoute == expectedRoute.Value;
        return new AiEvaluationDimensionResult(
            Name,
            passed ? AiEvaluationStatus.Passed : AiEvaluationStatus.Failed,
            $"Expected route={expectedRoute.Value}, actual route={actualRoute}.");
    }

    private static AssistantRoute InferRoute(OrchestratorResult result)
    {
        if (result.ExecutionSteps.Contains("ToolExecutor", StringComparer.Ordinal))
            return AssistantRoute.InvokeTools;

        if (result.ExecutionSteps.Contains("RAG", StringComparer.Ordinal))
            return AssistantRoute.UseRag;

        return AssistantRoute.DirectAnswer;
    }
}

public sealed class RetrievalRelevanceEvaluationDimension : IAiEvaluationDimension
{
    public string Name => "retrieval-relevance";

    public AiEvaluationDimensionResult Evaluate(AiEvaluationContext context)
    {
        var expectedRecipeId = context.Scenario.Expectations.ExpectedRetrievedRecipeId;
        if (string.IsNullOrWhiteSpace(expectedRecipeId))
            return new AiEvaluationDimensionResult(Name, AiEvaluationStatus.NotApplicable, "Scenario did not define an expected retrieved recipe.");

        var retrieval = context.Execution.LastRetrievalResult;
        if (retrieval is null)
            return new AiEvaluationDimensionResult(Name, AiEvaluationStatus.Failed, "Retrieval result was not captured.");

        var topSource = retrieval.Sources.FirstOrDefault();
        var passed = topSource is not null && string.Equals(topSource.RecipeId, expectedRecipeId, StringComparison.Ordinal);

        return new AiEvaluationDimensionResult(
            Name,
            passed ? AiEvaluationStatus.Passed : AiEvaluationStatus.Failed,
            $"Expected top source recipeId={expectedRecipeId}, actual recipeId={(topSource?.RecipeId ?? "none")}.");
    }
}

public sealed class SemanticSearchQualityEvaluationDimension : IAiEvaluationDimension
{
    public string Name => "semantic-search-quality";

    public AiEvaluationDimensionResult Evaluate(AiEvaluationContext context)
    {
        var expectedTopRecipeId = context.Scenario.Expectations.ExpectedTopSemanticRecipeId;
        if (string.IsNullOrWhiteSpace(expectedTopRecipeId))
            return new AiEvaluationDimensionResult(Name, AiEvaluationStatus.NotApplicable, "Scenario did not define a semantic-search expectation.");

        var results = context.Execution.SemanticSearchResults;
        if (results.Count == 0)
            return new AiEvaluationDimensionResult(Name, AiEvaluationStatus.Failed, "No semantic search results were captured.");

        var topMatch = results[0];
        var topMatchesExpected = string.Equals(topMatch.RecipeId, expectedTopRecipeId, StringComparison.Ordinal);
        var scoresAreDescending = results.Zip(results.Skip(1), (left, right) => left.Score >= right.Score).All(isDescending => isDescending);
        var passed = topMatchesExpected && scoresAreDescending;

        return new AiEvaluationDimensionResult(
            Name,
            passed ? AiEvaluationStatus.Passed : AiEvaluationStatus.Failed,
            $"Expected top recipeId={expectedTopRecipeId}, actual top recipeId={topMatch.RecipeId}, scoresDescending={scoresAreDescending}.");
    }
}

public sealed class ToolSelectionEvaluationDimension : IAiEvaluationDimension
{
    public string Name => "tool-selection";

    public AiEvaluationDimensionResult Evaluate(AiEvaluationContext context)
    {
        var expectedTools = context.Scenario.Expectations.ExpectedToolSelection;
        if (expectedTools.Count == 0)
            return new AiEvaluationDimensionResult(Name, AiEvaluationStatus.NotApplicable, "Scenario did not define expected tool selection.");

        var actualTools = context.Execution.LastOrchestratorResult.ExecutedTools;
        var passed = expectedTools.SequenceEqual(actualTools, StringComparer.Ordinal);

        return new AiEvaluationDimensionResult(
            Name,
            passed ? AiEvaluationStatus.Passed : AiEvaluationStatus.Failed,
            $"Expected tools=[{string.Join(",", expectedTools)}], actual tools=[{string.Join(",", actualTools)}].");
    }
}

public sealed class ToolExecutionEvaluationDimension : IAiEvaluationDimension
{
    public string Name => "tool-execution";

    public AiEvaluationDimensionResult Evaluate(AiEvaluationContext context)
    {
        var expectSuccess = context.Scenario.Expectations.ExpectSuccessfulToolExecution;
        if (expectSuccess is null)
            return new AiEvaluationDimensionResult(Name, AiEvaluationStatus.NotApplicable, "Scenario did not define tool execution expectations.");

        var invocations = context.Execution.ToolInvocations;
        if (invocations.Count == 0)
            return new AiEvaluationDimensionResult(Name, AiEvaluationStatus.Failed, "No tool execution invocations were captured.");

        var allSuccessful = invocations.All(invocation => invocation.Result.Success);
        var passed = expectSuccess.Value == allSuccessful;

        return new AiEvaluationDimensionResult(
            Name,
            passed ? AiEvaluationStatus.Passed : AiEvaluationStatus.Failed,
            $"Expected all tools successful={expectSuccess.Value}, actual all tools successful={allSuccessful}.");
    }
}

public sealed class MemoryRetrievalEvaluationDimension : IAiEvaluationDimension
{
    public string Name => "memory-retrieval";

    public AiEvaluationDimensionResult Evaluate(AiEvaluationContext context)
    {
        var expectations = context.Scenario.Expectations;
        if (expectations.ExpectMemoryRecall is null && expectations.MinimumSecondTurnMessageCount is null && string.IsNullOrWhiteSpace(expectations.RequiredPriorUserMessageInSecondTurn))
            return new AiEvaluationDimensionResult(Name, AiEvaluationStatus.NotApplicable, "Scenario did not define memory expectations.");

        var secondTurnMessageCount = context.Execution.LastOllamaMessages.Count;
        var minimumMessageCount = expectations.MinimumSecondTurnMessageCount ?? 0;
        var containsPriorMessage = true;
        if (!string.IsNullOrWhiteSpace(expectations.RequiredPriorUserMessageInSecondTurn))
        {
            containsPriorMessage = context.Execution.LastOllamaMessages.Any(message =>
                string.Equals(message.Content, expectations.RequiredPriorUserMessageInSecondTurn, StringComparison.Ordinal));
        }

        var memoryRecalled = secondTurnMessageCount >= minimumMessageCount && containsPriorMessage;
        if (expectations.ExpectMemoryRecall is null)
        {
            return new AiEvaluationDimensionResult(
                Name,
                memoryRecalled ? AiEvaluationStatus.Passed : AiEvaluationStatus.Failed,
                $"Observed second-turn messageCount={secondTurnMessageCount}, minimumExpected={minimumMessageCount}, containsPriorUserMessage={containsPriorMessage}.");
        }

        var passed = expectations.ExpectMemoryRecall.Value == memoryRecalled;
        return new AiEvaluationDimensionResult(
            Name,
            passed ? AiEvaluationStatus.Passed : AiEvaluationStatus.Failed,
            $"Expected memoryRecalled={expectations.ExpectMemoryRecall.Value}, actual memoryRecalled={memoryRecalled}, second-turn messageCount={secondTurnMessageCount}.");
    }
}

public sealed class GroundedResponseEvaluationDimension : IAiEvaluationDimension
{
    public string Name => "grounded-response";

    public AiEvaluationDimensionResult Evaluate(AiEvaluationContext context)
    {
        var expectedGrounded = context.Scenario.Expectations.ExpectGroundedResponse;
        if (expectedGrounded is null)
            return new AiEvaluationDimensionResult(Name, AiEvaluationStatus.NotApplicable, "Scenario did not define grounded-response expectation.");

        var response = context.Execution.LastChatResult.Response;
        var hasSourceBlock = response.Contains("Sources:", StringComparison.Ordinal);
        var passed = expectedGrounded.Value == hasSourceBlock;

        return new AiEvaluationDimensionResult(
            Name,
            passed ? AiEvaluationStatus.Passed : AiEvaluationStatus.Failed,
            $"Expected grounded response={expectedGrounded.Value}, actual has source block={hasSourceBlock}.");
    }
}

public sealed class OverallAnswerQualityEvaluationDimension : IAiEvaluationDimension
{
    public string Name => "overall-answer-quality";

    public AiEvaluationDimensionResult Evaluate(AiEvaluationContext context)
    {
        var expectations = context.Scenario.Expectations;
        var response = context.Execution.LastChatResult.Response;
        var responseNotBlank = !string.IsNullOrWhiteSpace(response);
        var errors = context.Execution.LastOrchestratorResult.Errors;
        var errorConstraint = !expectations.ExpectNoErrors || errors.Count == 0;
        var requiredFragmentsPresent = expectations.RequiredResponseFragments.All(fragment =>
            response.Contains(fragment, StringComparison.OrdinalIgnoreCase));

        var passed = responseNotBlank && errorConstraint && requiredFragmentsPresent;
        return new AiEvaluationDimensionResult(
            Name,
            passed ? AiEvaluationStatus.Passed : AiEvaluationStatus.Failed,
            $"responseNotBlank={responseNotBlank}, errorConstraint={errorConstraint}, requiredFragmentsPresent={requiredFragmentsPresent}, errorCount={errors.Count}.");
    }
}
