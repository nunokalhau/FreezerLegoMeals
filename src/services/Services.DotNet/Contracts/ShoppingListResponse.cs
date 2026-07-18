namespace Services.DotNet.Contracts
{
    public sealed class ShoppingListResponse
    {
        public IEnumerable<string> Recipes { get; init; }

        public int TotalRecipes { get; init; }

        public double ScaleFactor { get; init; }

        public IEnumerable<ShoppingListItem> Ingredients { get; init; }

        public string Message { get; init; }
    }

    public sealed class ShoppingListItem
    {
        public required string Name { get; init; }

        public required double Quantity { get; init; }

        public required string Unit { get; init; }

        public string? Category { get; init; }
    }
}
