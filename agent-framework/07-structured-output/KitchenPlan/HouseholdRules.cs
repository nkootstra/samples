static class HouseholdRules
{
    static readonly string[] Fish =
    [
        "salmon", "cod", "tuna", "haddock", "mackerel", "trout", "fish",
        "shrimp", "prawn", "crab", "mussel", "squid",
    ];

    public static List<string> Check(DinnerPlan plan)
    {
        List<string> problems = [];

        foreach (Dinner dinner in plan.Dinners)
        {
            if (dinner.MainIngredients.Any(i =>
                i.Contains("peanut", StringComparison.OrdinalIgnoreCase)))
            {
                problems.Add($"{dinner.Day}: contains peanuts.");
            }

            if (dinner.Minutes < 1)
            {
                problems.Add($"{dinner.Day}: takes {dinner.Minutes} minutes.");
            }

            bool weekday = dinner.Day
                is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
            if (weekday && dinner.Minutes >= 40)
            {
                problems.Add($"{dinner.Day}: takes {dinner.Minutes} " +
                    "minutes, weekday dinners must take under 40.");
            }
        }

        List<DayOfWeek> fishDays = plan.Dinners
            .Where(d => d.MainIngredients.Any(i => Fish.Any(f =>
                i.Contains(f, StringComparison.OrdinalIgnoreCase))))
            .Select(d => d.Day)
            .ToList();
        if (fishDays.Count > 1)
        {
            problems.Add($"Fish on {string.Join(", ", fishDays[..^1])} " +
                $"and {fishDays[^1]}: at most once a week.");
        }

        return problems;
    }
}
