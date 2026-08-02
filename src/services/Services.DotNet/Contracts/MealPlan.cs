namespace Services.DotNet.Contracts;

public sealed class MealPlan
{
    public IReadOnlyList<int> RecipeIds { get; init; } = new List<int>();
}
