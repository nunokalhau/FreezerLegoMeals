namespace WebApi.DotNet.Contracts.Responses;

/// <summary>
/// Flattened ingredient information for recipe search responses.
/// </summary>
public class RecipeSearchIngredientResponse
{
    public int IngredientId { get; set; }

    public double? Amount { get; set; }

    public string? Unit { get; set; }

    public string Name { get; set; } = string.Empty;
}
