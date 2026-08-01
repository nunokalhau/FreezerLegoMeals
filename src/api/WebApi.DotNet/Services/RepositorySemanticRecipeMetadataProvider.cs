using Domain.DotNet;
using Repository.DotNet;
using SemanticSearch.DotNet;
using Services.DotNet;

namespace WebApi.DotNet.Services;

public sealed class RepositorySemanticRecipeMetadataProvider : ISemanticRecipeMetadataProvider
{
    private readonly ILocalizedRecipeQueryService _localizedRecipeQueryService;
    private readonly ILanguageContextResolver _languageContextResolver;
    private readonly ILocalizationOptionsFactory _localizationOptionsFactory;
    private Dictionary<string, RecipeMetadata>? _cache;

    public RepositorySemanticRecipeMetadataProvider(
        ILocalizedRecipeQueryService localizedRecipeQueryService,
        ILanguageContextResolver languageContextResolver,
        ILocalizationOptionsFactory localizationOptionsFactory)
    {
        _localizedRecipeQueryService = localizedRecipeQueryService ?? throw new ArgumentNullException(nameof(localizedRecipeQueryService));
        _languageContextResolver = languageContextResolver ?? throw new ArgumentNullException(nameof(languageContextResolver));
        _localizationOptionsFactory = localizationOptionsFactory ?? throw new ArgumentNullException(nameof(localizationOptionsFactory));
    }

    public async Task<RecipeMetadata> GetMetadataAsync(string recipeId, CancellationToken cancellationToken = default)
    {
        if (_cache is null)
        {
            var languageContext = _languageContextResolver.Resolve(
                explicitLanguage: null,
                negotiatedLanguages: Array.Empty<string>(),
                defaultLanguage: "en");
            var options = _localizationOptionsFactory.Create(languageContext);
            var recipes = await _localizedRecipeQueryService.GetLocalizedRecipesAsync(options, cancellationToken);
            _cache = recipes.ToDictionary(
                recipe => recipe.CanonicalRecipeId.ToString(),
                recipe => new RecipeMetadata(
                    recipe.CanonicalRecipeId.ToString(),
                    recipe.Name,
                    string.Join(" | ", new[]
                    {
                        recipe.Name,
                        recipe.Notes,
                        recipe.Tags,
                        recipe.Prepping,
                        string.Join(", ", recipe.Ingredients.Select(ingredient => ingredient.Name).Where(name => !string.IsNullOrWhiteSpace(name)))
                    }.Where(value => !string.IsNullOrWhiteSpace(value))),
                    recipe.Notes ?? string.Empty,
                    recipe.Tags ?? string.Empty,
                    recipe.Ingredients.Select(ingredient => ingredient.Name).Where(name => !string.IsNullOrWhiteSpace(name)).ToList(),
                    recipe.Prepping ?? string.Empty,
                    recipe.TimeToPrepare?.ToString() ?? string.Empty));
        }

        return _cache.TryGetValue(recipeId, out var metadata)
            ? metadata
            : new RecipeMetadata(recipeId, $"Recipe {recipeId}", string.Empty);
    }
}