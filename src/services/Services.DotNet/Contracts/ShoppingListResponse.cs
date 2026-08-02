namespace Services.DotNet.Contracts;

public sealed class ShoppingListResponse
{
    public required MealPlan MealPlan { get; init; }

    public required ShoppingList ShoppingList { get; init; }

    public required FormattedShoppingList Formatted { get; init; }

    public int TotalRecipesInPlan { get; init; }

    public int TotalRecipesResolved { get; init; }

    public required string Message { get; init; }
}