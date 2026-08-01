using Domain.DotNet;
using Microsoft.EntityFrameworkCore;

namespace Repository.DotNet;

public interface ILocalizedRecipeQueryService
{
    Task<IReadOnlyList<LocalizedRecipe>> GetLocalizedRecipesAsync(
        LocalizationOptions options,
        CancellationToken cancellationToken = default);

    Task<LocalizedRecipe?> GetLocalizedRecipeByIdAsync(
        int canonicalRecipeId,
        LocalizationOptions options,
        CancellationToken cancellationToken = default);
}

public sealed class LocalizedRecipeQueryService : ILocalizedRecipeQueryService
{
    private readonly FreezerLegoMealsContext _context;

    public LocalizedRecipeQueryService(FreezerLegoMealsContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<LocalizedRecipe>> GetLocalizedRecipesAsync(
        LocalizationOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var recipes = await _context.Recipes
            .AsNoTracking()
            .Include(recipe => recipe.IndexMetadata)
            .Include(recipe => recipe.Translations)
            .Include(recipe => recipe.RecipeIngredients)
                .ThenInclude(recipeIngredient => recipeIngredient.Ingredient)
                    .ThenInclude(ingredient => ingredient.Translations)
            .OrderBy(recipe => recipe.Id)
            .ToListAsync(cancellationToken);

        return recipes
            .Select(recipe => MapToLocalizedRecipe(recipe, options))
            .Where(recipe => recipe is not null)
            .Select(recipe => recipe!)
            .ToList();
    }

    public async Task<LocalizedRecipe?> GetLocalizedRecipeByIdAsync(
        int canonicalRecipeId,
        LocalizationOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var recipe = await _context.Recipes
            .AsNoTracking()
            .Include(entity => entity.IndexMetadata)
            .Include(entity => entity.Translations)
            .Include(entity => entity.RecipeIngredients)
                .ThenInclude(recipeIngredient => recipeIngredient.Ingredient)
                    .ThenInclude(ingredient => ingredient.Translations)
            .FirstOrDefaultAsync(entity => entity.Id == canonicalRecipeId, cancellationToken);

        if (recipe is null)
            return null;

        return MapToLocalizedRecipe(recipe, options);
    }

    private static LocalizedRecipe? MapToLocalizedRecipe(Entities.RecipeEntity entity, LocalizationOptions options)
    {
        var candidateLanguages = BuildCandidateLanguages(options);
        var recipeTranslation = ResolveRecipeTranslation(entity, candidateLanguages, options.StrictMode);

        if (options.StrictMode && recipeTranslation is null)
        {
            return null;
        }

        var resolvedRecipeLanguage = recipeTranslation?.Language ?? options.PreferredLanguage;
        var fallbackLanguageUsed = recipeTranslation is null
            ? "canonical"
            : recipeTranslation.Language.Equals(options.PreferredLanguage, StringComparison.OrdinalIgnoreCase)
                ? null
                : recipeTranslation.Language;

        var ingredients = new List<LocalizedRecipeIngredient>(entity.RecipeIngredients.Count);
        foreach (var recipeIngredient in entity.RecipeIngredients)
        {
            var ingredientTranslation = ResolveIngredientTranslation(recipeIngredient, candidateLanguages, options.StrictMode);
            if (options.StrictMode && ingredientTranslation is null)
            {
                return null;
            }

            var ingredientLanguage = ingredientTranslation?.Language ?? resolvedRecipeLanguage;
            var ingredientName = ingredientTranslation?.Name ?? recipeIngredient.Ingredient?.Name ?? string.Empty;

            ingredients.Add(new LocalizedRecipeIngredient
            {
                CanonicalIngredientId = recipeIngredient.IngredientId,
                Language = ingredientLanguage,
                Name = ingredientName,
                Amount = recipeIngredient.Amount,
                Unit = ingredientTranslation?.Unit
                    ?? recipeIngredient.Unit
                    ?? recipeIngredient.Ingredient?.Unit
                    ?? string.Empty
            });
        }

        return new LocalizedRecipe
        {
            CanonicalRecipeId = entity.Id,
            Language = resolvedRecipeLanguage,
            FallbackLanguageUsed = fallbackLanguageUsed,
            Name = recipeTranslation?.Name ?? entity.Name ?? string.Empty,
            Tags = recipeTranslation?.Tags ?? entity.Tags ?? string.Empty,
            Notes = recipeTranslation?.Notes ?? entity.Notes ?? string.Empty,
            Prepping = recipeTranslation?.Prepping ?? entity.Prepping ?? string.Empty,
            TimeToPrepare = entity.TimeToPrepare,
            ProjectionSchemaVersion = entity.IndexMetadata?.ProjectionSchemaVersion ?? string.Empty,
            ProjectionFingerprint = entity.IndexMetadata?.ProjectionFingerprint ?? string.Empty,
            LanguageCoverage = entity.IndexMetadata?.LanguageCoverage ?? string.Empty,
            NormalizationVersion = "search-normalization-v1",
            Ingredients = ingredients
                .OrderBy(ingredient => ingredient.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
    }

    private static IReadOnlyList<string> BuildCandidateLanguages(LocalizationOptions options)
    {
        if (options.StrictMode)
        {
            return [options.PreferredLanguage];
        }

        var candidateLanguages = new List<string> { options.PreferredLanguage };
        candidateLanguages.AddRange(options.FallbackLanguages);
        return candidateLanguages;
    }

    private static Entities.RecipeTranslationEntity? ResolveRecipeTranslation(
        Entities.RecipeEntity recipe,
        IReadOnlyList<string> candidateLanguages,
        bool strictMode)
    {
        if (recipe.Translations.Count == 0)
        {
            return null;
        }

        foreach (var language in candidateLanguages)
        {
            var translation = recipe.Translations.FirstOrDefault(candidate =>
                candidate.Language.Equals(language, StringComparison.OrdinalIgnoreCase));
            if (translation is not null)
            {
                return translation;
            }
        }

        return strictMode ? null : recipe.Translations.OrderBy(candidate => candidate.Language, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
    }

    private static Entities.IngredientTranslationEntity? ResolveIngredientTranslation(
        Entities.RecipeIngredientEntity recipeIngredient,
        IReadOnlyList<string> candidateLanguages,
        bool strictMode)
    {
        if (recipeIngredient.Ingredient?.Translations is null || recipeIngredient.Ingredient.Translations.Count == 0)
        {
            return null;
        }

        foreach (var language in candidateLanguages)
        {
            var translation = recipeIngredient.Ingredient.Translations.FirstOrDefault(candidate =>
                candidate.Language.Equals(language, StringComparison.OrdinalIgnoreCase));
            if (translation is not null)
            {
                return translation;
            }
        }

        return strictMode
            ? null
            : recipeIngredient.Ingredient.Translations
                .OrderBy(candidate => candidate.Language, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
    }
}
