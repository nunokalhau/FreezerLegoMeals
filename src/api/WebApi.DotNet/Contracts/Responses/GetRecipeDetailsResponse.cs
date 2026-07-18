using Domain.DotNet;

namespace WebApi.DotNet.Contracts.Responses;

/// <summary>
/// Response DTO for getting detailed recipe information.
/// </summary>
public class GetRecipeDetailsResponse
{
    /// <summary>
    /// The recipe details.
    /// </summary>
    public required Recipe Recipe { get; set; }
    
    /// <summary>
    /// A descriptive message about the recipe.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}