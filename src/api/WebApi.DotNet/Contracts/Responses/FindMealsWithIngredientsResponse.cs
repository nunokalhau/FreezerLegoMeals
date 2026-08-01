namespace WebApi.DotNet.Contracts.Responses;

/// <summary>
/// Response DTO for finding meals with specified ingredients.
/// </summary>
public class FindMealsWithIngredientsResponse
{
    /// <summary>
    /// Original search query.
    /// </summary>
    public string Query { get; set; } = string.Empty;
    
    /// <summary>
    /// The search terms extracted from the query.
    /// </summary>
    public IEnumerable<string> SearchTerms { get; set; } = new List<string>();
    
    /// <summary>
    /// Total number of recipes found.
    /// </summary>
    public int TotalRecipesFound { get; set; }
    
    /// <summary>
    /// The list of matching recipes.
    /// </summary>
    public IEnumerable<RecipeSearchItemResponse> Recipes { get; set; } = new List<RecipeSearchItemResponse>();
    
    /// <summary>
    /// A descriptive message about the search results.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}