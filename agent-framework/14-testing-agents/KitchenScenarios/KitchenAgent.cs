using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

static class KitchenAgent
{
    public static AIAgent Create(IChatClient chatClient, Kitchen kitchen) =>
        chatClient.AsAIAgent(
            name: "kitchen-assistant",
            instructions: """
                You help one household with its pantry and shopping list.
                Check the pantry with your tools before you answer.
                Only add items to the shopping list when the household asks for it.
                Answer in one short sentence.
                """,
            tools:
            [
                AIFunctionFactory.Create(
                    kitchen.FindInPantry, name: "find_in_pantry"),
                AIFunctionFactory.Create(
                    kitchen.AddToShoppingList, name: "add_to_shopping_list"),
            ]);
}
