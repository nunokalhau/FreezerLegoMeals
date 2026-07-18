using System.ComponentModel.DataAnnotations;

namespace WebApi.DotNet.Contracts.Requests;

/// <summary>
/// Request DTO for getting recipe information by identifier.
/// </summary>
public class GetRecipeRequest
{
    /// <summary>
    /// Recipe name or ID to look up.
    /// </summary>
    [Required]
    public string Identifier { get; set; } = string.Empty;
}