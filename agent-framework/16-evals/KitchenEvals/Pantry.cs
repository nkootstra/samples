using System.ComponentModel;

static class Pantry
{
    public const string Contents =
        "rice 1 kg, eggs 6, onions 3, spinach 200 g, feta 150 g, "
        + "canned tomatoes 2 tins";

    [Description("Lists everything in the household's pantry.")]
    public static string ListPantry() => Contents;
}
