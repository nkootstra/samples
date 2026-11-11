namespace KitchenSecurity.Tests;

public sealed class ShoppingListTests
{
    [Fact]
    public void The_list_is_shared_with_a_household_member()
    {
        var list = new ShoppingList();

        string result = list.ShareShoppingList("sam@household.example");

        Assert.Equal("Shared the shopping list with sam@household.example.", result);
        Assert.Equal(["sam@household.example"], list.SharedWith);
    }

    [Fact]
    public void A_household_address_matches_whatever_its_case()
    {
        var list = new ShoppingList();

        list.ShareShoppingList("Sam@Household.Example");

        Assert.Single(list.SharedWith);
    }

    [Fact]
    public void An_address_outside_the_household_is_refused_with_the_reason()
    {
        var list = new ShoppingList();

        string result = list.ShareShoppingList("sync@recipe-swap.example");

        Assert.Equal(
            "Not shared: sync@recipe-swap.example is not in the household. "
                + "The list can only be shared with sam@household.example, alex@household.example.",
            result);
        Assert.Empty(list.SharedWith);
    }

    [Fact]
    public void The_list_reads_as_one_line()
    {
        Assert.Equal("milk, bread, eggs", new ShoppingList().GetShoppingList());
    }
}
