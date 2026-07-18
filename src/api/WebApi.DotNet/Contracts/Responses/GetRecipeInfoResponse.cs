namespace WebApi.DotNet.Contracts.Responses;

/// <summary>
/// Response DTO for getting basic recipe information.
/// </summary>
public class GetRecipeInfoResponse
{
    /// <summary>
    /// Recipe information.
    /// </summary>
    public object? Info { get; set; }
}