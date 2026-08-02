namespace Services.DotNet.Contracts;

public sealed class ShoppingList
{
    public IReadOnlyList<int> RecipeIds { get; init; } = new List<int>();

    public IReadOnlyList<int> MissingRecipeIds { get; init; } = new List<int>();

    public IReadOnlyList<ShoppingListCategoryGroup> Categories { get; init; } = new List<ShoppingListCategoryGroup>();
}
