using System.ComponentModel.DataAnnotations;

namespace FreezerLegoMeals.WebApi.DotNet.Contracts.Requests;

/// <summary>
/// Request DTO for getting a recipe by ID.
/// </summary>
public class GetRecipeByIdRequest
{
    /// <summary>
    /// The ID of the recipe to retrieve.
    /// </summary>
    [Required]
    public int Id { get; set; }
}