namespace WebApi.DotNet.Contracts.Responses;

/// <summary>
/// Localized recipe payload returned by localized recipe endpoints.
/// </summary>
public sealed class LocalizedRecipeResponse
{
    public int CanonicalRecipeId { get; set; }

    public string Language { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Tags { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public string Prepping { get; set; } = string.Empty;

    public int? TimeToPrepare { get; set; }

    public IReadOnlyList<LocalizedRecipeIngredientResponse> Ingredients { get; set; } = Array.Empty<LocalizedRecipeIngredientResponse>();
}
