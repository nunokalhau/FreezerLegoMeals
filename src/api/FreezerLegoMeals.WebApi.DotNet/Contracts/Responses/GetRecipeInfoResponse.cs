namespace FreezerLegoMeals.WebApi.DotNet.Contracts.Responses;

/// <summary>
/// Response DTO for getting basic recipe information.
/// </summary>
public class GetRecipeInfoResponse
{
    /// <summary>
    /// Recipe information.
    /// </summary>
    public object? Info { get; set; }
    
    /// <summary>
    /// Indicates whether the recipe was found.
    /// </summary>
    public bool Found { get; set; }
    
    /// <summary>
    /// Error information if the recipe was not found.
    /// </summary>
    public string? Error { get; set; }
}