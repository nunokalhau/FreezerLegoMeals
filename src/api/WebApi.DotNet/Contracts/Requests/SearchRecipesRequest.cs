using System.ComponentModel.DataAnnotations;

namespace WebApi.DotNet.Contracts.Requests;

/// <summary>
/// Request DTO for searching recipes by ingredients.
/// </summary>
public class SearchRecipesRequest
{
    /// <summary>
    /// The list of ingredients to search for.
    /// </summary>
    [Required]
    public IEnumerable<string> Ingredients { get; set; } = new List<string>();
}