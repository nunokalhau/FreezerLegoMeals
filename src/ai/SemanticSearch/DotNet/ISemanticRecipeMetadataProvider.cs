namespace SemanticSearch.DotNet;

public interface ISemanticRecipeMetadataProvider
{
    Task<RecipeMetadata> GetMetadataAsync(string recipeId, CancellationToken cancellationToken = default);
}