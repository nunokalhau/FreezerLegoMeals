using System.ComponentModel.DataAnnotations;
using Services.DotNet.Contracts;

namespace WebApi.DotNet.Contracts.Requests;

/// <summary>
/// Request DTO for generating a shopping list.
/// </summary>
public class GenerateShoppingListRequest
{
    /// <summary>
    /// Structured meal plan containing only recipe IDs.
    /// </summary>
    [Required]
    public MealPlan MealPlan { get; set; } = new();
}