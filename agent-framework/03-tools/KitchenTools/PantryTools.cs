using System.ComponentModel;

sealed record PantryItem(string Amount, DateOnly UseBy);

sealed class PantryTools
{
    static readonly DateOnly Today =
        DateOnly.FromDateTime(DateTime.Today);

    readonly Dictionary<string, PantryItem> _items =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["rice"] = new("1 kg", Today.AddMonths(6)),
            ["eggs"] = new("6", Today.AddDays(9)),
            ["onions"] = new("3", Today.AddDays(20)),
            ["spinach"] = new("200 g", Today.AddDays(1)),
            ["feta"] = new("150 g", Today.AddDays(3)),
        };

    [Description("Looks up one ingredient in the household's pantry.")]
    public string FindInPantry(
        [Description(
            "One ingredient as a plain noun, for example eggs.")]
        string ingredient)
    {
        if (_items.TryGetValue(ingredient, out PantryItem? item))
        {
            return $"Today is {Today:O}. {ingredient}: " +
                $"{item.Amount}, use by {item.UseBy:O}.";
        }

        return $"No {ingredient} in the pantry. " +
            $"The pantry has: {string.Join(", ", _items.Keys)}.";
    }

    [Description("Lists pantry items that expire soon, soonest first.")]
    public string ListExpiringSoon(
        [Description("How many days ahead to look, from 1 to 14.")]
        int days)
    {
        if (days is < 1 or > 14)
        {
            return $"days must be 1 to 14, got {days}. " +
                "Call again with a value in that range.";
        }

        var expiring = _items
            .Where(i => i.Value.UseBy <= Today.AddDays(days))
            .OrderBy(i => i.Value.UseBy)
            .Select(i => $"{i.Key} ({i.Value.Amount}, " +
                $"use by {i.Value.UseBy:O})")
            .ToList();

        return expiring.Count == 0
            ? $"Nothing expires in the next {days} " +
                (days == 1 ? "day." : "days.")
            : $"Today is {Today:O}. Expiring: " +
                string.Join("; ", expiring) + ".";
    }
}
