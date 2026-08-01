namespace WebApi.DotNet.Contracts.Responses;

/// <summary>
/// Localized ingredient payload for a localized recipe response.
/// </summary>
public sealed class LocalizedRecipeIngredientResponse
{
    public int CanonicalIngredientId { get; set; }

    public string Language { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public double? Amount { get; set; }

    public string Unit { get; set; } = string.Empty;
}
