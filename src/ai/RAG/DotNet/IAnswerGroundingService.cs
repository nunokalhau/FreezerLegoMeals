namespace RAG.DotNet;

public interface IAnswerGroundingService
{
    Task<AnswerGroundingResult> ValidateAsync(
        string answer,
        IReadOnlyList<RetrievalRecipe> retrievedRecipes,
        CancellationToken cancellationToken = default);
}

public sealed record AnswerGroundingResult(bool Grounded, int UnsupportedClaimsCount);