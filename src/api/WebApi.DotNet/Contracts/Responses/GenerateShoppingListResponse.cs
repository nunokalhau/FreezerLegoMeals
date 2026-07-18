namespace WebApi.DotNet.Contracts.Responses;

/// <summary>
/// Response DTO for generating a shopping list.
/// </summary>
public class GenerateShoppingListResponse
{
    /// <summary>
    /// The generated shopping list data.
    /// </summary>
    public object ShoppingList { get; set; } = new();
    
    /// <summary>
    /// A descriptive message about the shopping list.
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// The scaling factor used for ingredient amounts.
    /// </summary>
    public double ScaleFactor { get; set; } = 1.0;
    
    /// <summary>
    /// Whether ingredients were grouped by category.
    /// </summary>
    public bool GroupByCategory { get; set; } = true;
}