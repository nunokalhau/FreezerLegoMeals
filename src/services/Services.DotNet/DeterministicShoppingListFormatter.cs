using System.Globalization;
using Services.DotNet.Contracts;

namespace Services.DotNet;

public sealed class DeterministicShoppingListFormatter : IShoppingListFormatter
{
    private const string QuantityNotSpecifiedPt = "quantidade nao especificada";
    private const string QuantityNotSpecifiedEn = "quantity not specified";

    public FormattedShoppingList Format(ShoppingList shoppingList, string languageCode = "pt")
    {
        ArgumentNullException.ThrowIfNull(shoppingList);

        var isPortuguese = languageCode.StartsWith("pt", StringComparison.OrdinalIgnoreCase);
        var lines = new List<string>();

        foreach (var category in shoppingList.Categories)
        {
            lines.Add($"[{category.Category}]");

            foreach (var item in category.Items)
            {
                var line = BuildItemLine(item, isPortuguese);
                lines.Add($"- {line}");
            }

            lines.Add(string.Empty);
        }

        if (shoppingList.MissingRecipeIds.Count > 0)
        {
            var missingLabel = isPortuguese ? "Receitas nao encontradas" : "Missing recipes";
            var missing = string.Join(", ", shoppingList.MissingRecipeIds.OrderBy(id => id));
            lines.Add($"{missingLabel}: {missing}");
        }

        return new FormattedShoppingList
        {
            Lines = lines
        };
    }

    private static string BuildItemLine(ShoppingListIngredient item, bool isPortuguese)
    {
        var quantityNotSpecifiedText = isPortuguese ? QuantityNotSpecifiedPt : QuantityNotSpecifiedEn;
        var quantityText = item.Quantity.HasValue
            ? item.Quantity.Value.ToString("0.###", CultureInfo.InvariantCulture)
            : quantityNotSpecifiedText;

        var unitText = string.IsNullOrWhiteSpace(item.Unit) ? string.Empty : $" {item.Unit}";
        var baseLine = $"{item.Name}: {quantityText}{unitText}";

        if (item.UnspecifiedQuantityOccurrences <= 0)
        {
            return baseLine;
        }

        var suffix = isPortuguese
            ? $" (+{item.UnspecifiedQuantityOccurrences} com {quantityNotSpecifiedText})"
            : $" (+{item.UnspecifiedQuantityOccurrences} with {quantityNotSpecifiedText})";

        return baseLine + suffix;
    }
}
