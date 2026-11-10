using Microsoft.Extensions.AI;

namespace KitchenScenarios.Tests;

// The checks decide what counts as a pass, so they are tested like any other code.
public sealed class ScenarioCheckTests
{
    static string? Check(string scenario, string[] list, params string[] calls) =>
        Scenarios.All.Single(s => s.Name == scenario).Check(new ScenarioRun(
            [.. calls.Select(name => new FunctionCallContent("call", name))],
            list));

    [Fact]
    public void Milk_alone_on_the_list_passes()
    {
        Assert.Null(Check("milk on the list", ["milk"], "add_to_shopping_list"));
    }

    [Theory]
    [InlineData("Milk")]
    [InlineData("whole milk")]
    public void Milk_in_any_case_or_kind_passes(string milk)
    {
        Assert.Null(Check("milk on the list", [milk], "add_to_shopping_list"));
    }

    [Fact]
    public void A_word_that_only_contains_milk_fails()
    {
        Assert.NotNull(Check("milk on the list", ["buttermilk"], "add_to_shopping_list"));
    }

    [Fact]
    public void Milk_with_anything_else_fails()
    {
        Assert.NotNull(Check("milk on the list", ["milk", "eggs"], "add_to_shopping_list"));
    }

    [Fact]
    public void A_question_answered_without_looking_in_the_pantry_fails()
    {
        Assert.Equal("never checked the pantry", Check("question only", []));
    }

    [Fact]
    public void A_question_that_adds_to_the_list_fails()
    {
        Assert.Equal(
            "added to the list without being asked",
            Check("question only", ["eggs"], "find_in_pantry", "add_to_shopping_list"));
    }

    [Fact]
    public void A_question_answered_from_the_pantry_passes()
    {
        Assert.Null(Check("question only", [], "find_in_pantry"));
    }

    [Fact]
    public void A_replacement_for_peanut_butter_fails()
    {
        Assert.NotNull(Check("no peanuts", ["almond butter"], "add_to_shopping_list"));
    }

    [Fact]
    public void Any_kind_of_pasta_and_extras_pass()
    {
        Assert.Null(Check("missing for a recipe", ["spaghetti", "garlic"], "add_to_shopping_list"));
    }

    [Theory]
    [InlineData("tagliatelle")]
    [InlineData("orzo")]
    [InlineData("macaroni")]
    [InlineData("Rigatoni")]
    [InlineData("whole-wheat penne")]
    public void Common_pasta_shapes_count_as_pasta(string pasta)
    {
        Assert.Null(Check("missing for a recipe", [pasta], "add_to_shopping_list"));
    }

    [Fact]
    public void An_item_that_only_contains_a_pantry_word_is_not_in_the_pantry()
    {
        Assert.Null(Check("missing for a recipe", ["penne", "eggplant"], "add_to_shopping_list"));
    }

    [Fact]
    public void A_list_without_pasta_fails()
    {
        Assert.Equal(
            "no pasta on the list",
            Check("missing for a recipe", ["garlic"], "add_to_shopping_list"));
    }

    [Fact]
    public void Buying_what_the_pantry_already_has_fails()
    {
        Assert.Equal(
            "feta is already in the pantry",
            Check("missing for a recipe", ["pasta", "feta"], "add_to_shopping_list"));
    }

    [Theory]
    [InlineData("Eggs")]
    [InlineData("egg")]
    [InlineData("red onions")]
    public void A_pantry_item_in_any_case_or_number_fails(string item)
    {
        Assert.Equal(
            $"{item} is already in the pantry",
            Check("missing for a recipe", ["pasta", item], "add_to_shopping_list"));
    }
}
