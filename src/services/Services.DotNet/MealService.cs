using Domain.DotNet;
using Repository.DotNet;
using Services.DotNet.Contracts;
using System.Text.RegularExpressions;

namespace Services.DotNet;

/// <summary>
/// Provides business logic for meal-related operations.
/// </summary>
public class MealService : IMealService
{
    private readonly IRecipeRepository _recipeRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="MealService"/> class.
    /// </summary>
    /// <param name="recipeRepository">The recipe repository to use for data access.</param>
    public MealService(IRecipeRepository recipeRepository)
    {
        _recipeRepository = recipeRepository ?? throw new ArgumentNullException(nameof(recipeRepository));
    }

    /// <summary>
    /// Search for recipes containing any of the specified ingredients.
    /// </summary>
    /// <param name="ingredients">List of ingredient names to search for</param>
    /// <returns>List of matching recipes</returns>
    public async Task<IEnumerable<Recipe>> SearchRecipesByIngredientsAsync(IEnumerable<string> ingredients)
    {
        ArgumentNullException.ThrowIfNull(ingredients);

        return await _recipeRepository.FindRecipesWithIngredientsAsync(ingredients);
    }

    /// <summary>
    /// Get a specific recipe by ID.
    /// </summary>
    /// <param name="id">The recipe ID</param>
    /// <returns>The recipe if found, null otherwise</returns>
    public async Task<Recipe?> GetRecipeByIdAsync(int id)
    {
        return await _recipeRepository.GetRecipeByIdAsync(id);
    }

    /// <summary>
    /// Search for recipes containing specified ingredients and return detailed information.
    /// </summary>
    /// <param name="query">Natural language query about meals/recipes</param>
    /// <returns>Detailed search results</returns>
    public async Task<IngredientSearchResponse> FindMealsWithIngredientsAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentNullException(nameof(query));

        // This would involve more complex parsing based on the Python implementation
        // For now, we'll just pass it through to the repository for finding recipes with ingredients
        var ingredients = ExtractFoodTermsFromQuery(query);
        var recipes = await _recipeRepository.FindRecipesWithIngredientsAsync(ingredients);
        
        return new IngredientSearchResponse
        {
            Query = query,
            SearchTerms = ingredients,
            TotalRecipesFound = recipes?.Count() ?? 0,
            Recipes = recipes,
            Message = $"Found {recipes?.Count() ?? 0} recipes containing the specified ingredients"
        };
    }

    /// <summary>
    /// Get detailed information about a specific recipe.
    /// </summary>
    /// <param name="id">The recipe ID</param>
    /// <returns>Detailed recipe information</returns>
    public async Task<RecipeDetailsResponse> GetRecipeDetailsAsync(int id)
    {
        var recipe = await _recipeRepository.GetRecipeByIdAsync(id);
        
        if (recipe == null)
        {
            return new RecipeDetailsResponse
            {
                Error = $"No recipe found with ID {id}"
            };
        }
        
        return new RecipeDetailsResponse
        {
            Query = $"Recipe details for {recipe.Name}",
            Recipe = recipe,
            Message = $"Details for recipe: {recipe.Name}"
        };
    }

    /// <summary>
    /// Extract food terms from a natural language query.
    /// </summary>
    /// <param name="query">Natural language query</param>
    /// <returns>List of extracted ingredient terms</returns>
    private IEnumerable<string> ExtractFoodTermsFromQuery(string query)
    {
        return Regex.Matches(query, "[\\p{L}\\p{Nd}]+", RegexOptions.CultureInvariant)
            .Select(match => match.Value.ToLowerInvariant())
            .Where(term => term.Length >= 3)
            .Distinct()
            .ToList();
    }
}