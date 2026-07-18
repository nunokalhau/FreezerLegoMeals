using Domain.DotNet;
using Services.DotNet.Contracts;

namespace Services.DotNet;

/// <summary>
/// Interface for shopping list generation and management.
/// </summary>
public interface IShoppingService
{
    /// <summary>
    /// Get all ingredients for a specified recipe by name or ID.
    /// </summary>
    /// <param name="recipeIdentifier">Recipe name or ID</param>
    /// <returns>List of ingredient dictionaries with name, amount, unit, and other details</returns>
    Task<IEnumerable<RecipeIngredient>> GetRecipeIngredientsAsync(string recipeIdentifier);
    
    /// <summary>
    /// Get ingredients for multiple recipes.
    /// </summary>
    /// <param name="recipeIdentifiers">List of recipe names or IDs</param>
    /// <returns>Dictionary mapping recipe names to their ingredients</returns>
    Task<Dictionary<string, IEnumerable<RecipeIngredient>>> GetMultipleRecipeIngredientsAsync(IEnumerable<string> recipeIdentifiers);
    
    /// <summary>
    /// Generate a shopping list from one or more recipes.
    /// </summary>
    /// <param name="recipeIdentifiers">List of recipe names or IDs to include</param>
    /// <param name="scaleFactor">Factor to scale ingredient amounts (e.g., 2.0 for double servings)</param>
    /// <param name="groupByCategory">Whether to group ingredients by category</param>
    /// <returns>Dictionary with shopping list data and metadata</returns>
    Task<ShoppingListResponse> GenerateShoppingListAsync(IEnumerable<string> recipeIdentifiers, 
                                            double scaleFactor = 1.0, 
                                            bool groupByCategory = true);
    
    /// <summary>
    /// Get basic information about a specific recipe.
    /// </summary>
    /// <param name="recipeIdentifier">Recipe name or ID</param>
    /// <returns>Recipe information dictionary or null if not found</returns>
    Task<RecipeInfoResponse?> GetRecipeInfoAsync(string recipeIdentifier);
}