using System.ComponentModel.DataAnnotations;

namespace WebApi.DotNet.Contracts.Requests;

/// <summary>
/// Request DTO for generating a shopping list.
/// </summary>
public class GenerateShoppingListRequest
{
    /// <summary>
    /// List of recipe names or IDs to include in the shopping list.
    /// </summary>
    [Required]
    public IEnumerable<string> RecipeIdentifiers { get; set; } = new List<string>();
    
    /// <summary>
    /// Factor to scale ingredient amounts (e.g., 2.0 for double servings).
    /// </summary>
    public double ScaleFactor { get; set; } = 1.0;
    
    /// <summary>
    /// Whether to group ingredients by category.
    /// </summary>
    public bool GroupByCategory { get; set; } = true;
}