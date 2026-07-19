using Services.DotNet;

namespace Orchestration.DotNet;

public sealed record RetrievedRecipeInfo(string RecipeId, string Title, double SimilarityScore);

public sealed record OrchestratorResult(
    string FinalResponse,
    string SelectedAgent,
    IReadOnlyList<string> ExecutedTools,
    IReadOnlyList<RetrievedRecipeInfo> RetrievedRecipes,
    IReadOnlyList<string> ExecutionSteps,
    TimeSpan ExecutionDuration,
    IReadOnlyList<string> Errors,
    IReadOnlyList<ConversationMessage> MessagesToPersist);