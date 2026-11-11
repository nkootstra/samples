using System.ComponentModel;

sealed class ShoppingList
{
    public static readonly string[] Household =
        ["sam@household.example", "alex@household.example"];

    readonly List<string> _items = ["milk", "bread", "eggs"];

    public List<string> SharedWith { get; } = [];

    [Description("Returns the household's shopping list.")]
    public string GetShoppingList() => string.Join(", ", _items);

    [Description("Shares the shopping list with someone by email.")]
    public string ShareShoppingList(
        [Description("The email address to share it with.")]
        string email)
    {
        if (!Household.Contains(email, StringComparer.OrdinalIgnoreCase))
        {
            return $"Not shared: {email} is not in the household. "
                + "The list can only be shared with "
                + $"{string.Join(", ", Household)}.";
        }

        SharedWith.Add(email);
        return $"Shared the shopping list with {email}.";
    }
}
