using Domain.DotNet;
using Repository.DotNet;
using Services.DotNet.Contracts;

namespace Services.DotNet;

public sealed class DeterministicShoppingListGenerator : IShoppingListGenerator
{
    private const string UnknownCategory = "uncategorized";

    private readonly IRecipeRepository _recipeRepository;

    public DeterministicShoppingListGenerator(IRecipeRepository recipeRepository)
    {
        _recipeRepository = recipeRepository ?? throw new ArgumentNullException(nameof(recipeRepository));
    }

    public async Task<ShoppingList> GenerateAsync(MealPlan mealPlan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mealPlan);

        var recipeIds = mealPlan.RecipeIds
            .Where(id => id > 0)
            .ToList();

        if (recipeIds.Count == 0)
        {
            return new ShoppingList();
        }

        var foundRecipes = new List<Recipe>();
        var missingRecipeIds = new List<int>();

        foreach (var recipeId in recipeIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var recipe = await _recipeRepository.GetRecipeByIdAsync(recipeId);
            if (recipe is null)
            {
                missingRecipeIds.Add(recipeId);
                continue;
            }

            foundRecipes.Add(recipe);
        }

        var aggregated = new Dictionary<string, IngredientAccumulator>(StringComparer.OrdinalIgnoreCase);

        foreach (var recipe in foundRecipes)
        {
            foreach (var ingredient in recipe.RecipeIngredients)
            {
                if (ingredient.Ingredient is null || string.IsNullOrWhiteSpace(ingredient.Ingredient.Name))
                {
                    continue;
                }

                var category = ResolveCategory(recipe.SourcePath);
                var unit = ingredient.Unit?.Trim() ?? string.Empty;
                var ingredientName = ingredient.Ingredient.Name.Trim();
                var key = BuildAggregationKey(category, ingredientName, unit);

                if (!aggregated.TryGetValue(key, out var accumulator))
                {
                    accumulator = new IngredientAccumulator(category, ingredientName, unit);
                    aggregated[key] = accumulator;
                }

                if (ingredient.Amount.HasValue)
                {
                    accumulator.Quantity += ingredient.Amount.Value;
                    accumulator.HasAnyQuantity = true;
                }
                else
                {
                    accumulator.UnspecifiedQuantityOccurrences++;
                }
            }
        }

        var groupedCategories = aggregated.Values
            .GroupBy(item => item.Category, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ShoppingListCategoryGroup
            {
                Category = group.Key,
                Items = group
                    .OrderBy(item => item.IngredientName, StringComparer.OrdinalIgnoreCase)
                    .Select(item => new ShoppingListIngredient
                    {
                        Name = item.IngredientName,
                        Unit = item.Unit,
                        Quantity = item.HasAnyQuantity ? item.Quantity : null,
                        UnspecifiedQuantityOccurrences = item.UnspecifiedQuantityOccurrences
                    })
                    .ToList()
            })
            .ToList();

        return new ShoppingList
        {
            RecipeIds = recipeIds,
            MissingRecipeIds = missingRecipeIds,
            Categories = groupedCategories
        };
    }

    private static string ResolveCategory(string? sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return UnknownCategory;
        }

        var tokens = sourcePath
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var foodIndex = Array.FindIndex(tokens, token => string.Equals(token, "food", StringComparison.OrdinalIgnoreCase));
        if (foodIndex >= 0 && foodIndex + 1 < tokens.Length)
        {
            return tokens[foodIndex + 1].ToLowerInvariant();
        }

        if (tokens.Length >= 2)
        {
            return tokens[^2].ToLowerInvariant();
        }

        return UnknownCategory;
    }

    private static string BuildAggregationKey(string category, string ingredientName, string unit)
    {
        return $"{category}||{ingredientName}||{unit}";
    }

    private sealed class IngredientAccumulator
    {
        public IngredientAccumulator(string category, string ingredientName, string unit)
        {
            Category = category;
            IngredientName = ingredientName;
            Unit = unit;
        }

        public string Category { get; }

        public string IngredientName { get; }

        public string Unit { get; }

        public double Quantity { get; set; }

        public bool HasAnyQuantity { get; set; }

        public int UnspecifiedQuantityOccurrences { get; set; }
    }
}
