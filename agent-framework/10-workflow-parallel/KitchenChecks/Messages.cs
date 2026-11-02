sealed record PlanDraft(string Request, DinnerPlan Plan, int Round);

sealed record RuleCheck(string Rule, List<string> Problems, PlanDraft Draft);

sealed record Review(PlanDraft Draft, List<string> Problems)
{
    public bool Passed => Problems.Count == 0;
}

sealed record PeanutFindings(List<PeanutFinding> Findings);

sealed record PeanutFinding(DayOfWeek Day, string Reason);

static class PlanText
{
    public static string Lines(DinnerPlan plan) =>
        string.Join("\n", plan.Dinners.Select(d =>
            $"{d.Day}: {d.Dish}, {d.Minutes} min " +
            $"({string.Join(", ", d.MainIngredients)})"));
}
