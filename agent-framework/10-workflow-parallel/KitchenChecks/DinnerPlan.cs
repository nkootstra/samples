using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

[Description("Dinners for the days the household asked about.")]
sealed record DinnerPlan(
    [property: Description("One dinner per requested day, in order.")]
    List<Dinner> Dinners);

sealed record Dinner(
    DayOfWeek Day,
    [property: Description("The dish in a few words.")]
    string Dish,
    [property: Description("Minutes from start to plate.")]
    [property: Range(1, 240)]
    int Minutes,
    [property: Description(
        "Main ingredients as plain nouns, for example salmon.")]
    List<string> MainIngredients);
