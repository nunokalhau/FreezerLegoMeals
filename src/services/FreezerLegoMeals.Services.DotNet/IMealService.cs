using System.Collections.Generic;
using System.Threading.Tasks;
using FreezerLegoMeals.Domain.DotNet;
using FreezerLegoMeals.Repository.DotNet;

namespace FreezerLegoMeals.Services.DotNet;

/// <summary>
/// Interface for meal-related service operations.
/// </summary>
public interface IMealService
{
    /// <summary>
    /// Search for recipes containing any of the specified ingredients.
    /// </summary>
    /// <param name="ingredients">List of ingredient names to search for</param>
    /// <returns>List of matching recipes</returns>
    Task<IEnumerable<Recipe>> SearchRecipesByIngredientsAsync(IEnumerable<string> ingredients);
    
    /// <summary>
    /// Get a specific recipe by ID.
    /// </summary>
    /// <param name="id">The recipe ID</param>
    /// <returns>The recipe if found, null otherwise</returns>
    Task<Recipe> GetRecipeByIdAsync(int id);
    
    /// <summary>
    /// Search for recipes containing specified ingredients and return detailed information.
    /// </summary>
    /// <param name="query">Natural language query about meals/recipes</param>
    /// <returns>Detailed search results</returns>
    Task<object> FindMealsWithIngredientsAsync(string query);
    
    /// <summary>
    /// Get detailed information about a specific recipe.
    /// </summary>
    /// <param name="id">The recipe ID</param>
    /// <returns>Detailed recipe information</returns>
    Task<object> GetRecipeDetailsAsync(int id);
}