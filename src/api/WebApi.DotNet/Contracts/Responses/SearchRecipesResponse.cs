namespace WebApi.DotNet.Contracts.Responses;

/// <summary>
/// Response DTO for searching recipes by ingredients.
/// </summary>
public class SearchRecipesResponse
{
    /// <summary>
    /// List of matching recipes.
    /// </summary>
    public IEnumerable<RecipeSearchItemResponse> Recipes { get; set; } = new List<RecipeSearchItemResponse>();
    
    /// <summary>
    /// Total number of recipes found.
    /// </summary>
    public int TotalRecipesFound { get; set; }
}