namespace Domain.DotNet;

public sealed class LocalizedRecipeIngredient
{
    public int CanonicalIngredientId { get; init; }

    public string Language { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public double? Amount { get; init; }

    public string Unit { get; init; } = string.Empty;
}
