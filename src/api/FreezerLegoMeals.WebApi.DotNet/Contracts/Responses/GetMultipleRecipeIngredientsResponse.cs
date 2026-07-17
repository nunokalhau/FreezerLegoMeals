using FreezerLegoMeals.Domain.DotNet;

namespace FreezerLegoMeals.WebApi.DotNet.Contracts.Responses;

/// <summary>
/// Response DTO for getting ingredients for multiple recipes.
/// </summary>
public class GetMultipleRecipeIngredientsResponse
{
    /// <summary>
    /// Dictionary mapping recipe names to their ingredients.
    /// </summary>
    public Dictionary<string, IEnumerable<RecipeIngredient>> RecipeIngredients { get; set; } = new();
    
    /// <summary>
    /// Total number of recipes processed.
    /// </summary>
    public int TotalRecipes { get; set; }
    
    /// <summary>
    /// Indicates whether any recipes were found.
    /// </summary>
    public bool Found { get; set; }
}