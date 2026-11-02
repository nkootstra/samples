namespace KitchenChecks.Tests;

public sealed class HouseholdRulesTests
{
    static PlanDraft Draft(params Dinner[] dinners) =>
        new("Dinners for this week.", new DinnerPlan([.. dinners]), Round: 1);

    static Dinner Dinner(DayOfWeek day, int minutes, params string[] ingredients) =>
        new(day, "A dish", minutes, [.. ingredients]);

    [Fact]
    public void Fish_on_one_day_is_fine()
    {
        RuleCheck check = HouseholdRules.CheckFish(Draft(
            Dinner(DayOfWeek.Monday, 25, "salmon", "rice"),
            Dinner(DayOfWeek.Wednesday, 30, "chicken", "pasta")));

        Assert.Empty(check.Problems);
    }

    [Fact]
    public void Fish_on_two_days_is_one_problem_naming_both_days()
    {
        RuleCheck check = HouseholdRules.CheckFish(Draft(
            Dinner(DayOfWeek.Monday, 25, "Salmon fillet"),
            Dinner(DayOfWeek.Wednesday, 30, "chicken"),
            Dinner(DayOfWeek.Saturday, 50, "cod")));

        Assert.Equal(["Fish on Monday and Saturday: at most once a week."], check.Problems);
    }

    [Fact]
    public void A_weekday_dinner_of_40_minutes_or_more_is_a_problem()
    {
        RuleCheck check = HouseholdRules.CheckWeekdayTime(Draft(
            Dinner(DayOfWeek.Friday, 40, "chicken")));

        Assert.Equal(
            ["Friday: takes 40 minutes, weekday dinners must take under 40."],
            check.Problems);
    }

    [Fact]
    public void A_long_dinner_at_the_weekend_is_fine()
    {
        RuleCheck check = HouseholdRules.CheckWeekdayTime(Draft(
            Dinner(DayOfWeek.Sunday, 180, "lamb")));

        Assert.Empty(check.Problems);
    }

    [Fact]
    public void A_dinner_of_under_one_minute_is_a_problem_on_any_day()
    {
        RuleCheck check = HouseholdRules.CheckWeekdayTime(Draft(
            Dinner(DayOfWeek.Sunday, 0, "lamb")));

        Assert.Equal(["Sunday: takes 0 minutes."], check.Problems);
    }

    [Fact]
    public void A_check_carries_the_plan_it_checked()
    {
        PlanDraft draft = Draft(Dinner(DayOfWeek.Monday, 25, "salmon"));

        Assert.Same(draft, HouseholdRules.CheckFish(draft).Draft);
        Assert.Same(draft, HouseholdRules.CheckWeekdayTime(draft).Draft);
    }
}
