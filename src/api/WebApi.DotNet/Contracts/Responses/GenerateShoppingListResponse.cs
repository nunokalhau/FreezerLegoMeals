using Services.DotNet.Contracts;

namespace WebApi.DotNet.Contracts.Responses;

/// <summary>
/// Response DTO for generating a shopping list.
/// </summary>
public class GenerateShoppingListResponse
{
    /// <summary>
    /// The generated shopping list data.
    /// </summary>
    public required ShoppingListResponse ShoppingList { get; set; }
    
    /// <summary>
    /// A descriptive message about the shopping list.
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// Indicates this payload was generated deterministically from stored recipes.
    /// </summary>
    public bool Deterministic { get; set; } = true;
}