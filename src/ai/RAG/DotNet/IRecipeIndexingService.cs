namespace RAG.DotNet;

public interface IRecipeIndexingService
{
    Task<RecipeIndexingResult> IndexAllRecipesAsync(CancellationToken cancellationToken = default);
}
