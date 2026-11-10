using System.ComponentModel;

sealed class Kitchen
{
    readonly Dictionary<string, string> _pantry =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["rice"] = "1 kg",
            ["eggs"] = "6",
            ["onions"] = "3",
            ["spinach"] = "200 g",
            ["feta"] = "150 g",
        };

    readonly List<string> _shoppingList = [];

    public IReadOnlyList<string> ShoppingList => _shoppingList;

    [Description("Looks up one ingredient in the household's pantry.")]
    public string FindInPantry(
        [Description(
            "One ingredient as a plain noun, for example eggs.")]
        string ingredient)
    {
        if (_pantry.TryGetValue(ingredient, out string? amount))
        {
            return $"{ingredient}: {amount}.";
        }

        return $"No {ingredient} in the pantry. " +
            $"The pantry has: {string.Join(", ", _pantry.Keys)}.";
    }

    [Description("Adds one item to the household's shopping list.")]
    public string AddToShoppingList(
        [Description("One item as a plain noun, for example milk.")]
        string item)
    {
        string name = item.Trim().ToLowerInvariant();

        if (name.Length == 0)
        {
            return "Not added: the item is empty. " +
                "Name one item, for example milk.";
        }

        if (name.Contains("peanut", StringComparison.Ordinal))
        {
            return $"Not added: {name}. " +
                "Nobody in this household eats peanuts.";
        }

        if (_shoppingList.Contains(name))
        {
            return $"{name} is already on the shopping list.";
        }

        _shoppingList.Add(name);
        return $"Added {name}. " +
            $"The list now has: {string.Join(", ", _shoppingList)}.";
    }
}
