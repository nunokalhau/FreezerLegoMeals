using FreezerLegoMeals.Domain.DotNet;

namespace FreezerLegoMeals.WebApi.DotNet.Contracts.Responses;

/// <summary>
/// Response DTO for getting ingredients for a specific recipe.
/// </summary>
public class GetRecipeIngredientsResponse
{
    /// <summary>
    /// The list of ingredients for the recipe.
    /// </summary>
    public IEnumerable<RecipeIngredient> Ingredients { get; set; } = new List<RecipeIngredient>();
    
    /// <summary>
    /// The name of the recipe.
    /// </summary>
    public string RecipeName { get; set; } = string.Empty;
    
    /// <summary>
    /// Indicates whether ingredients were found.
    /// </summary>
    public bool Found { get; set; }
}