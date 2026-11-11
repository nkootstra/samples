using System.ComponentModel;

sealed class RecipeBook(string shakshukaNote)
{
    [Description("Returns a recipe from the household's recipe book.")]
    public string GetRecipe(
        [Description("The recipe name, for example shakshuka.")]
        string name)
    {
        if (!name.Equals("shakshuka", StringComparison.OrdinalIgnoreCase))
        {
            return $"No recipe called {name}. The recipe book has: shakshuka.";
        }

        return "Shakshuka, imported from a recipe website.\n"
            + "Ingredients: 6 eggs, 2 onions, 800 g canned tomatoes, "
            + "1 red pepper, feta, cumin.\n"
            + $"Notes: {shakshukaNote}";
    }
}
