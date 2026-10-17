using System.Globalization;
using System.Text.RegularExpressions;

namespace KitchenTools.Tests;

public sealed class PantryToolsTests
{
    readonly PantryTools _pantry = new();

    // PantryTools reads today's date once, the first time the class is used.
    // The expected dates count from the date the tool reports, so a test run
    // that crosses midnight still compares against the same day.
    static readonly DateOnly Today = DateOnly.ParseExact(
        Regex.Match(new PantryTools().ListExpiringSoon(14), @"^Today is (\S+)\.").Groups[1].Value,
        "O",
        CultureInfo.InvariantCulture);

    static string DaysFromToday(int days) => Today.AddDays(days).ToString("O", CultureInfo.InvariantCulture);

    [Fact]
    public void An_ingredient_in_the_pantry_comes_with_today_its_amount_and_use_by_date()
    {
        string result = _pantry.FindInPantry("eggs");

        Assert.Equal($"Today is {DaysFromToday(0)}. eggs: 6, use by {DaysFromToday(9)}.", result);
    }

    [Fact]
    public void The_lookup_ignores_case()
    {
        string result = _pantry.FindInPantry("Eggs");

        Assert.Equal($"Today is {DaysFromToday(0)}. Eggs: 6, use by {DaysFromToday(9)}.", result);
    }

    [Fact]
    public void A_missing_ingredient_lists_what_the_pantry_has()
    {
        string result = _pantry.FindInPantry("egg");

        Assert.Equal(
            "No egg in the pantry. The pantry has: rice, eggs, onions, spinach, feta.",
            result);
    }

    [Fact]
    public void Expiring_items_start_with_today_and_come_soonest_first()
    {
        string result = _pantry.ListExpiringSoon(3);

        Assert.Equal(
            $"Today is {DaysFromToday(0)}. Expiring: "
                + $"spinach (200 g, use by {DaysFromToday(1)}); "
                + $"feta (150 g, use by {DaysFromToday(3)}).",
            result);
    }

    [Fact]
    public void Dates_stay_yyyy_MM_dd_in_a_culture_with_another_calendar()
    {
        CultureInfo culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("th-TH");
        try
        {
            string result = _pantry.FindInPantry("eggs");

            Assert.Equal($"Today is {DaysFromToday(0)}. eggs: 6, use by {DaysFromToday(9)}.", result);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(30)]
    public void A_day_count_outside_1_to_14_is_answered_with_what_to_do_instead(int days)
    {
        string result = _pantry.ListExpiringSoon(days);

        Assert.Equal(
            $"days must be 1 to 14, got {days}. Call again with a value in that range.",
            result);
    }
}
