namespace KitchenPlan.Tests;

public sealed class HouseholdRulesTests
{
    static Dinner Dinner(DayOfWeek day, int minutes, params string[] ingredients) =>
        new(day, "A dish", minutes, [.. ingredients]);

    [Fact]
    public void A_plan_that_keeps_every_rule_has_no_problems()
    {
        DinnerPlan plan = new(
        [
            Dinner(DayOfWeek.Monday, 25, "salmon", "rice"),
            Dinner(DayOfWeek.Wednesday, 35, "chicken", "pasta"),
            Dinner(DayOfWeek.Saturday, 90, "beef", "potatoes"),
        ]);

        Assert.Empty(HouseholdRules.Check(plan));
    }

    [Fact]
    public void Peanuts_in_any_dinner_are_a_problem()
    {
        DinnerPlan plan = new([Dinner(DayOfWeek.Tuesday, 20, "noodles", "Peanut butter")]);

        Assert.Equal(["Tuesday: contains peanuts."], HouseholdRules.Check(plan));
    }

    [Fact]
    public void A_weekday_dinner_of_40_minutes_or_more_is_a_problem()
    {
        DinnerPlan plan = new([Dinner(DayOfWeek.Friday, 40, "chicken")]);

        Assert.Equal(
            ["Friday: takes 40 minutes, weekday dinners must take under 40."],
            HouseholdRules.Check(plan));
    }

    [Fact]
    public void A_long_dinner_at_the_weekend_is_fine()
    {
        DinnerPlan plan = new([Dinner(DayOfWeek.Sunday, 180, "lamb")]);

        Assert.Empty(HouseholdRules.Check(plan));
    }

    [Fact]
    public void Fish_on_two_days_is_one_problem_naming_both_days()
    {
        DinnerPlan plan = new(
        [
            Dinner(DayOfWeek.Monday, 25, "salmon", "rice"),
            Dinner(DayOfWeek.Wednesday, 20, "chicken"),
            Dinner(DayOfWeek.Saturday, 60, "Cod fillets", "potatoes"),
        ]);

        Assert.Equal(
            ["Fish on Monday and Saturday: at most once a week."],
            HouseholdRules.Check(plan));
    }

    [Fact]
    public void Fish_on_three_days_names_them_in_one_sentence()
    {
        DinnerPlan plan = new(
        [
            Dinner(DayOfWeek.Monday, 25, "salmon"),
            Dinner(DayOfWeek.Wednesday, 20, "tuna"),
            Dinner(DayOfWeek.Saturday, 60, "trout"),
        ]);

        Assert.Equal(
            ["Fish on Monday, Wednesday and Saturday: at most once a week."],
            HouseholdRules.Check(plan));
    }

    [Fact]
    public void Shellfish_counts_as_fish()
    {
        DinnerPlan plan = new(
        [
            Dinner(DayOfWeek.Monday, 25, "salmon", "rice"),
            Dinner(DayOfWeek.Wednesday, 20, "Shrimp", "broccoli"),
        ]);

        Assert.Equal(
            ["Fish on Monday and Wednesday: at most once a week."],
            HouseholdRules.Check(plan));
    }

    [Fact]
    public void A_dinner_without_minutes_is_a_problem()
    {
        DinnerPlan plan = new([Dinner(DayOfWeek.Saturday, 0, "eggs")]);

        Assert.Equal(["Saturday: takes 0 minutes."], HouseholdRules.Check(plan));
    }
}
