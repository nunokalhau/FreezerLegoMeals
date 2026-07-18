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
    public Recipe? Recipe { get; set; }
    
    /// <summary>
    /// A descriptive message about the recipe.
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// Indicates whether the recipe was found.
    /// </summary>
    public bool Found { get; set; }
    
    /// <summary>
    /// Error information if the recipe was not found.
    /// </summary>
    public string? Error { get; set; }
}