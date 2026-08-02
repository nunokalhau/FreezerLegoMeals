using Services.DotNet.Contracts;

namespace Services.DotNet;

public interface IShoppingListFormatter
{
    FormattedShoppingList Format(ShoppingList shoppingList, string languageCode = "pt");
}
