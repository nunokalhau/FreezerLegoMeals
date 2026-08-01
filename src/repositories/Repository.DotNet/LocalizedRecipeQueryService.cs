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
            .Include(recipe => recipe.RecipeIngredients)
                .ThenInclude(recipeIngredient => recipeIngredient.Ingredient)
            .OrderBy(recipe => recipe.Id)
            .ToListAsync(cancellationToken);

        return recipes
            .Select(recipe => MapToLocalizedRecipe(recipe, options.PreferredLanguage))
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
            .Include(entity => entity.RecipeIngredients)
                .ThenInclude(recipeIngredient => recipeIngredient.Ingredient)
            .FirstOrDefaultAsync(entity => entity.Id == canonicalRecipeId, cancellationToken);

        if (recipe is null)
            return null;

        return MapToLocalizedRecipe(recipe, options.PreferredLanguage);
    }

    private static LocalizedRecipe MapToLocalizedRecipe(Entities.RecipeEntity entity, string preferredLanguage)
    {
        return new LocalizedRecipe
        {
            CanonicalRecipeId = entity.Id,
            Language = preferredLanguage,
            FallbackLanguageUsed = null,
            Name = entity.Name ?? string.Empty,
            Tags = entity.Tags ?? string.Empty,
            Notes = entity.Notes ?? string.Empty,
            Prepping = entity.Prepping ?? string.Empty,
            TimeToPrepare = entity.TimeToPrepare,
            Ingredients = entity.RecipeIngredients
                .Select(recipeIngredient => new LocalizedRecipeIngredient
                {
                    CanonicalIngredientId = recipeIngredient.IngredientId,
                    Language = preferredLanguage,
                    Name = recipeIngredient.Ingredient?.Name ?? string.Empty,
                    Amount = recipeIngredient.Amount,
                    Unit = recipeIngredient.Unit ?? string.Empty
                })
                .OrderBy(ingredient => ingredient.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
    }
}
