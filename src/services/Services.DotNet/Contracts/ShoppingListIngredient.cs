namespace Services.DotNet.Contracts;

public sealed class ShoppingListIngredient
{
    public required string Name { get; init; }

    public string Unit { get; init; } = string.Empty;

    public double? Quantity { get; init; }

    public int UnspecifiedQuantityOccurrences { get; init; }
}
