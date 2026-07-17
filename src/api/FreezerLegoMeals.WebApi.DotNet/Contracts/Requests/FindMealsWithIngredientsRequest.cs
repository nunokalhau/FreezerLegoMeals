using System.ComponentModel.DataAnnotations;

namespace FreezerLegoMeals.WebApi.DotNet.Contracts.Requests;

/// <summary>
/// Request DTO for finding meals with specified ingredients.
/// </summary>
public class FindMealsWithIngredientsRequest
{
    /// <summary>
    /// Natural language query about meals/recipes.
    /// </summary>
    [Required]
    public string Query { get; set; } = string.Empty;
}