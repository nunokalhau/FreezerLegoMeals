namespace WebApi.DotNet.Contracts.Responses;

/// <summary>
/// Flattened recipe payload for ingredient search endpoints.
/// </summary>
public class RecipeSearchItemResponse
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string SourcePath { get; set; } = string.Empty;

    public string Tags { get; set; } = string.Empty;

    public int? Servings { get; set; }

    public int? TimeToPrepare { get; set; }

    public string Prepping { get; set; } = string.Empty;

    public string FreezingNotes { get; set; } = string.Empty;

    public string ReheatNotes { get; set; } = string.Empty;

    public string Combinations { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public IEnumerable<RecipeSearchIngredientResponse> RecipeIngredients { get; set; } = new List<RecipeSearchIngredientResponse>();
}
