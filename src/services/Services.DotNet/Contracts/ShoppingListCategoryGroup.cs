namespace Services.DotNet.Contracts;

public sealed class ShoppingListCategoryGroup
{
    public required string Category { get; init; }

    public IReadOnlyList<ShoppingListIngredient> Items { get; init; } = new List<ShoppingListIngredient>();
}
