using Domain.DotNet;

namespace WebApi.DotNet.Contracts.Responses;

/// <summary>
/// Response DTO for getting a recipe by ID.
/// </summary>
public class GetRecipeByIdResponse
{
    /// <summary>
    /// The recipe details.
    /// </summary>
    public required Recipe Recipe { get; set; }
}