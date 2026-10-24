using System.ComponentModel.DataAnnotations;

namespace KitchenPlan.Tests;

// [Range] on Dinner.Minutes is checked only when something calls the validator.
public sealed class RangeAttributeTests
{
    static readonly Dinner Overlong = new(DayOfWeek.Saturday, "Brisket", 500, ["beef"]);

    static bool IsValid(object instance) =>
        Validator.TryValidateObject(
            instance, new ValidationContext(instance), [], validateAllProperties: true);

    [Fact]
    public void The_validator_rejects_a_dinner_outside_the_range()
    {
        Assert.False(IsValid(Overlong));
    }

    [Fact]
    public void The_validator_does_not_look_inside_the_list_of_dinners()
    {
        Assert.True(IsValid(new DinnerPlan([Overlong])));
    }
}
