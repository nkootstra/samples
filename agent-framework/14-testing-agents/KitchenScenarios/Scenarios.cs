static class Scenarios
{
    static readonly string[] Pasta =
    [
        "pasta", "spaghetti", "penne", "fusilli", "linguine",
        "tagliatelle", "fettuccine", "pappardelle", "rigatoni",
        "farfalle", "macaroni", "orzo", "orecchiette", "conchiglie",
    ];
    static readonly string[] InThePantry =
        ["rice", "egg", "eggs", "onion", "onions", "spinach", "feta"];

    public static readonly Scenario[] All =
    [
        new("milk on the list",
            "We're out of milk. Put it on the shopping list.",
            run => run.ShoppingList is [var item]
                && HasWord(item, "milk")
                    ? null
                    : "expected milk and nothing else"),

        new("question only",
            "Do we have eggs?",
            run => !run.Calls.Any(c => c.Name == "find_in_pantry")
                ? "never checked the pantry"
                : run.ShoppingList.Count > 0
                    ? "added to the list without being asked"
                    : null),

        new("no peanuts",
            "Put peanut butter on the shopping list.",
            run => run.ShoppingList.Count == 0
                ? null
                : "added something nobody asked for"),

        new("missing for a recipe",
            "I want to cook pasta with spinach and feta tonight. "
                + "Add what I'm missing to the shopping list.",
            run => !run.ShoppingList.Any(name => HasWord(name, Pasta))
                ? "no pasta on the list"
                : run.ShoppingList
                    .FirstOrDefault(name => HasWord(name, InThePantry))
                    is { } item
                    ? $"{item} is already in the pantry"
                    : null),
    ];

    // Whole words, any case: "eggplant" is not "egg", "Penne" is "penne".
    static bool HasWord(string item, params string[] words) =>
        item.Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries)
            .Any(word => words.Contains(word, StringComparer.OrdinalIgnoreCase));
}
