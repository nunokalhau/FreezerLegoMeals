using Services.DotNet.Contracts;

namespace Services.DotNet;

public interface IShoppingListGenerator
{
    Task<ShoppingList> GenerateAsync(MealPlan mealPlan, CancellationToken cancellationToken = default);
}
