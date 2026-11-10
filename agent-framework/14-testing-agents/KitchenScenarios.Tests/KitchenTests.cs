namespace KitchenScenarios.Tests;

// Layer 1: the code the model never decides. No agent, no chat client.
public sealed class KitchenTests
{
    readonly Kitchen _kitchen = new();

    [Fact]
    public void An_ingredient_in_the_pantry_comes_back_with_its_amount()
    {
        Assert.Equal("eggs: 6.", _kitchen.FindInPantry("eggs"));
    }

    [Fact]
    public void The_pantry_lookup_ignores_case()
    {
        Assert.Equal("Eggs: 6.", _kitchen.FindInPantry("Eggs"));
    }

    [Fact]
    public void A_missing_ingredient_lists_what_the_pantry_has()
    {
        Assert.Equal(
            "No milk in the pantry. The pantry has: rice, eggs, onions, spinach, feta.",
            _kitchen.FindInPantry("milk"));
    }

    [Fact]
    public void The_shopping_list_starts_empty()
    {
        Assert.Empty(_kitchen.ShoppingList);
    }

    [Fact]
    public void An_added_item_is_stored_trimmed_and_in_lower_case()
    {
        string result = _kitchen.AddToShoppingList(" Milk ");

        Assert.Equal(["milk"], _kitchen.ShoppingList);
        Assert.Equal("Added milk. The list now has: milk.", result);
    }

    [Fact]
    public void An_item_already_on_the_list_is_not_added_twice()
    {
        _kitchen.AddToShoppingList("milk");

        string result = _kitchen.AddToShoppingList("MILK");

        Assert.Equal(["milk"], _kitchen.ShoppingList);
        Assert.Equal("milk is already on the shopping list.", result);
    }

    [Theory]
    [InlineData("peanut butter")]
    [InlineData("Salted Peanuts")]
    public void Anything_with_peanuts_is_refused_with_the_reason(string item)
    {
        string result = _kitchen.AddToShoppingList(item);

        Assert.Empty(_kitchen.ShoppingList);
        Assert.StartsWith("Not added: ", result);
        Assert.EndsWith("Nobody in this household eats peanuts.", result);
    }

    [Fact]
    public void An_empty_item_is_refused()
    {
        string result = _kitchen.AddToShoppingList("  ");

        Assert.Empty(_kitchen.ShoppingList);
        Assert.Equal("Not added: the item is empty. Name one item, for example milk.", result);
    }
}
