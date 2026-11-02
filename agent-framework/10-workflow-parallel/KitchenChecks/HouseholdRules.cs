static class HouseholdRules
{
    static readonly string[] Fish =
        ["salmon", "cod", "tuna", "haddock", "mackerel", "trout", "fish"];

    public static RuleCheck CheckFish(PlanDraft draft)
    {
        List<DayOfWeek> fishDays = draft.Plan.Dinners
            .Where(d => d.MainIngredients.Any(i => Fish.Any(f =>
                i.Contains(f, StringComparison.OrdinalIgnoreCase))))
            .Select(d => d.Day)
            .ToList();

        List<string> problems = fishDays.Count > 1
            ? [$"Fish on {string.Join(", ", fishDays[..^1])} " +
                $"and {fishDays[^1]}: at most once a week."]
            : [];
        return new RuleCheck("fish", problems, draft);
    }

    public static RuleCheck CheckWeekdayTime(PlanDraft draft)
    {
        List<string> problems = draft.Plan.Dinners
            .Where(d => d.Minutes < 1 || (d.Minutes >= 40
                && d.Day is not (DayOfWeek.Saturday or DayOfWeek.Sunday)))
            .Select(d => d.Minutes < 1
                ? $"{d.Day}: takes {d.Minutes} minutes."
                : $"{d.Day}: takes {d.Minutes} minutes, " +
                    "weekday dinners must take under 40.")
            .ToList();
        return new RuleCheck("weekday-time", problems, draft);
    }
}
