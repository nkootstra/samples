enum RequestKind { QuickDinner, WeekPlan, SomethingElse }

sealed record Triage(RequestKind Kind);

sealed record DinnerRequest(string Text, RequestKind Kind);

static class PlanText
{
    public static string Lines(DinnerPlan plan) =>
        string.Join("\n", plan.Dinners.Select(d =>
            $"{d.Day}: {d.Dish}, {d.Minutes} min " +
            $"({string.Join(", ", d.MainIngredients)})"));
}
