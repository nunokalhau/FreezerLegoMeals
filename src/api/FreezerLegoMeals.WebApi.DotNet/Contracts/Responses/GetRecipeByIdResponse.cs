using FreezerLegoMeals.Domain.DotNet;

namespace FreezerLegoMeals.WebApi.DotNet.Contracts.Responses;

/// <summary>
/// Response DTO for getting a recipe by ID.
/// </summary>
public class GetRecipeByIdResponse
{
    /// <summary>
    /// The recipe details.
    /// </summary>
    public Recipe? Recipe { get; set; }
    
    /// <summary>
    /// Indicates whether the recipe was found.
    /// </summary>
    public bool Found { get; set; }
}