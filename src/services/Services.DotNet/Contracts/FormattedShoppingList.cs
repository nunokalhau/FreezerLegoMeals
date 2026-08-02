namespace Services.DotNet.Contracts;

public sealed class FormattedShoppingList
{
    public IReadOnlyList<string> Lines { get; init; } = new List<string>();
}
