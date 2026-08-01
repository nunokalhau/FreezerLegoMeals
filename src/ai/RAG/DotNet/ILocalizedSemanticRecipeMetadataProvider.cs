using Domain.DotNet;
using SemanticSearch.DotNet;

namespace RAG.DotNet;

public interface ILocalizedSemanticRecipeMetadataProvider
{
    Task<RecipeMetadata?> GetMetadataAsync(
        string recipeId,
        LocalizationOptions localizationOptions,
        CancellationToken cancellationToken = default);
}
