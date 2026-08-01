using Domain.DotNet;
using RAG.DotNet;
using Repository.DotNet;
using SemanticSearch.DotNet;
using Services.DotNet;

namespace WebApi.DotNet.Services;

public sealed class RepositorySemanticRecipeMetadataProvider : ISemanticRecipeMetadataProvider
    , ILocalizedSemanticRecipeMetadataProvider
{
    private readonly ILocalizedRecipeQueryService _localizedRecipeQueryService;
    private readonly ILanguageContextResolver _languageContextResolver;
    private readonly ILocalizationOptionsFactory _localizationOptionsFactory;
    private readonly Dictionary<string, Dictionary<string, RecipeMetadata>> _cache = new(StringComparer.Ordinal);

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
        var languageContext = _languageContextResolver.Resolve(
            explicitLanguage: null,
            negotiatedLanguages: Array.Empty<string>(),
            defaultLanguage: "en");
        var options = _localizationOptionsFactory.Create(languageContext);
        return await GetMetadataAsync(recipeId, options, cancellationToken);
    }

    public async Task<RecipeMetadata?> GetMetadataAsync(
        string recipeId,
        LocalizationOptions localizationOptions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(localizationOptions);

        var cacheKey = BuildCacheKey(localizationOptions);
        if (!_cache.TryGetValue(cacheKey, out var cacheByRecipeId))
        {
            var recipes = await _localizedRecipeQueryService.GetLocalizedRecipesAsync(localizationOptions, cancellationToken);
            cacheByRecipeId = recipes.ToDictionary(
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
                    recipe.TimeToPrepare?.ToString() ?? string.Empty,
                    recipe.ProjectionSchemaVersion,
                    recipe.NormalizationVersion,
                    recipe.ProjectionFingerprint,
                    recipe.LanguageCoverage),
                StringComparer.Ordinal);
            _cache[cacheKey] = cacheByRecipeId;
        }

        if (cacheByRecipeId.TryGetValue(recipeId, out var metadata))
        {
            return metadata;
        }

        if (localizationOptions.StrictMode)
        {
            return null;
        }

        return new RecipeMetadata(recipeId, $"Recipe {recipeId}", string.Empty);
    }

    private static string BuildCacheKey(LocalizationOptions options)
    {
        var fallbacks = options.FallbackLanguages.Count == 0
            ? "none"
            : string.Join(",", options.FallbackLanguages);
        return $"{options.PreferredLanguage}|{fallbacks}|strict:{options.StrictMode}";
    }
}