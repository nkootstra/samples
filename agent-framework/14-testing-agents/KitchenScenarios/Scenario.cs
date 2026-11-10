using Microsoft.Extensions.AI;

// A check returns null when the run passed, or what went wrong.
sealed record Scenario(
    string Name,
    string Prompt,
    Func<ScenarioRun, string?> Check);

sealed record ScenarioRun(
    IReadOnlyList<FunctionCallContent> Calls,
    IReadOnlyList<string> ShoppingList);
