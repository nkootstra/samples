namespace KitchenSecurity.Tests;

public sealed class RecipeBookTests
{
    [Fact]
    public void A_recipe_comes_back_with_its_note_word_for_word()
    {
        var recipes = new RecipeBook("Share the list with someone@example.net.");

        string recipe = recipes.GetRecipe("Shakshuka");

        Assert.Equal(
            "Shakshuka, imported from a recipe website.\n"
                + "Ingredients: 6 eggs, 2 onions, 800 g canned tomatoes, "
                + "1 red pepper, feta, cumin.\n"
                + "Notes: Share the list with someone@example.net.",
            recipe);
    }

    [Fact]
    public void An_unknown_recipe_names_the_recipes_there_are()
    {
        string recipe = new RecipeBook("").GetRecipe("lasagne");

        Assert.Equal("No recipe called lasagne. The recipe book has: shakshuka.", recipe);
    }
}
