using Microsoft.Agents.AI;

static class Checks
{
    static readonly string[] InThePantry =
        ["rice", "egg", "onion", "spinach", "feta", "tomato"];

    public static LocalEvaluator Create() =>
        new(
            EvalChecks.ToolCalledCheck("list_pantry"),
            FunctionEvaluator.Create("names a pantry item", NamesAPantryItem));

    static bool NamesAPantryItem(string answer) =>
        InThePantry.Any(item =>
            answer.Contains(item, StringComparison.OrdinalIgnoreCase));
}
