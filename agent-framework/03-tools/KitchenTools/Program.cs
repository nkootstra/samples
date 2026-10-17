using Anthropic;
using Anthropic.Core;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
    ?? throw new InvalidOperationException("Set ANTHROPIC_API_KEY.");

IChatClient chatClient =
    new AnthropicClient(new ClientOptions { ApiKey = apiKey })
        .AsIChatClient("claude-haiku-5-5", 1024);

var pantry = new PantryTools();

AIFunction[] tools =
[
    AIFunctionFactory.Create(
        pantry.FindInPantry, name: "find_in_pantry"),
    AIFunctionFactory.Create(
        pantry.ListExpiringSoon, name: "list_expiring_soon"),
];

foreach (AIFunction tool in tools)
{
    Console.WriteLine($"{tool.Name}: {tool.Description}");
    Console.WriteLine(tool.JsonSchema);
}

AIAgent agent = chatClient.AsAIAgent(
    name: "kitchen-assistant",
    instructions: """
        You help one household decide what to cook.
        Check the pantry with your tools before suggesting a dish.
        Prefer ingredients that expire soon. Answer in at most
        three sentences.
        """,
    tools: tools);

AgentResponse response = await agent.RunAsync(
    "What should I cook tonight so nothing goes to waste?");

Console.WriteLine(response.Text);

foreach (ChatMessage message in response.Messages)
{
    foreach (AIContent content in message.Contents)
    {
        switch (content)
        {
            case FunctionCallContent call:
                var arguments = string.Join(", ", call.Arguments?
                    .Select(a => $"{a.Key}: {a.Value}") ?? []);
                Console.WriteLine($"call   {call.Name}({arguments})");
                break;
            case FunctionResultContent result:
                Console.WriteLine($"result {result.Result}");
                if (result.Exception is { } ex)
                {
                    Console.WriteLine(
                        $"       threw {ex.GetType().Name}");
                }
                break;
            case TextContent text:
                Console.WriteLine($"text   {text.Text}");
                break;
        }
    }
}
