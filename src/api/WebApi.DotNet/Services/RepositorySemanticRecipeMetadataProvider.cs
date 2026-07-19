using Repository.DotNet;
using SemanticSearch.DotNet;

namespace WebApi.DotNet.Services;

public sealed class RepositorySemanticRecipeMetadataProvider : ISemanticRecipeMetadataProvider
{
    private readonly IRecipeRepository _recipeRepository;
    private Dictionary<string, RecipeMetadata>? _cache;

    public RepositorySemanticRecipeMetadataProvider(IRecipeRepository recipeRepository)
    {
        _recipeRepository = recipeRepository ?? throw new ArgumentNullException(nameof(recipeRepository));
    }

    public async Task<RecipeMetadata> GetMetadataAsync(string recipeId, CancellationToken cancellationToken = default)
    {
        if (_cache is null)
        {
            var recipes = await _recipeRepository.GetRecipesAsync();
            _cache = recipes.ToDictionary(
                recipe => recipe.Id.ToString(),
                recipe => new RecipeMetadata(
                    recipe.Id.ToString(),
                    recipe.Name,
                    string.Join(" | ", new[]
                    {
                        recipe.Name,
                        recipe.Notes,
                        recipe.Tags,
                        recipe.Prepping,
                        string.Join(", ", recipe.RecipeIngredients.Select(ingredient => ingredient.Ingredient?.Name).Where(name => !string.IsNullOrWhiteSpace(name)))
                    }.Where(value => !string.IsNullOrWhiteSpace(value)))));
        }

        return _cache.TryGetValue(recipeId, out var metadata)
            ? metadata
            : new RecipeMetadata(recipeId, $"Recipe {recipeId}", string.Empty);
    }
}