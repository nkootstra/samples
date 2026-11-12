using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

static class KitchenAgent
{
    public static AIAgent Create(IChatClient chatClient) =>
        chatClient.AsAIAgent(
            name: "kitchen-assistant",
            instructions: """
                You suggest dinners for one household.
                Check the pantry with list_pantry before you answer.
                Suggest one dish that uses what is in the pantry, and
                name anything the household would have to buy.
                Answer in at most three sentences.
                """,
            tools:
            [
                AIFunctionFactory.Create(
                    Pantry.ListPantry, name: "list_pantry"),
            ]);
}
