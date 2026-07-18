namespace Services.DotNet.Contracts;

public sealed class ShoppingListItem
{
    public required string Name { get; init; }

    public required double Quantity { get; init; }

    public required string Unit { get; init; }

    public string? Category { get; init; }
}
