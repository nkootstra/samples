using System.ComponentModel;
using System.Text.Encodings.Web;
using System.Text.Json;
using Anthropic;
using Anthropic.Core;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
    ?? throw new InvalidOperationException("Set ANTHROPIC_API_KEY.");

IChatClient chatClient =
    new AnthropicClient(new ClientOptions { ApiKey = apiKey })
        .AsIChatClient("claude-haiku-5-5", 1024);

List<string> shoppingList = ["milk", "bread"];

[Description("Returns the household's shopping list.")]
string GetShoppingList() => string.Join(", ", shoppingList);

[Description("Adds one item to the household's shopping list.")]
string AddToShoppingList(
    [Description("The item, for example 'eggs'.")]
    string item,
    [Description("How much, for example '12' or '500 g'.")]
    string quantity)
{
    shoppingList.Add($"{quantity} {item}");
    return $"Added {quantity} {item}. " +
        $"The list has {shoppingList.Count} items.";
}

AIAgent agent = chatClient.AsAIAgent(
    name: "kitchen-assistant",
    instructions: """
        You help one household with cooking and shopping.
        Answer in at most three sentences.
        """,
    tools:
    [
        AIFunctionFactory.Create(
            GetShoppingList, name: "get_shopping_list"),
        new ApprovalRequiredAIFunction(
            AIFunctionFactory.Create(
                AddToShoppingList, name: "add_to_shopping_list")),
    ]);

AgentSession session = await agent.CreateSessionAsync();
AgentResponse response = await agent.RunAsync(
    "We're out of eggs, put a dozen on the list.", session);

List<ToolApprovalRequestContent> requests = Pending(response);
Console.WriteLine($"Waiting for approval: {requests.Count}");
Console.WriteLine($"Shopping list: {string.Join(", ", shoppingList)}");

const string Rejected =
    "The person reviewed this call and turned it down, so nothing changed. " +
    "Ask them what they would like instead of proposing it again.";
const string MovedOn =
    "The person sent a new message instead, so this was not done.";

JsonSerializerOptions readable = new()
{
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
};

while (requests.Count > 0)
{
    foreach (ToolApprovalRequestContent request in requests)
    {
        var call = (FunctionCallContent)request.ToolCall;
        Console.WriteLine(
            $"{call.Name} {JsonSerializer.Serialize(call.Arguments, readable)}");
    }

    Console.Write("Approve? (y/n, or type a new message) ");
    string answer = Console.ReadLine()?.Trim() ?? "";

    List<ChatMessage> reply = answer.ToLowerInvariant() switch
    {
        "y" => [Answer(requests, true, null)],
        "n" or "" => [Answer(requests, false, Rejected)],
        _ =>
        [
            Answer(requests, false, MovedOn),
            new(ChatRole.User, answer),
        ],
    };

    response = await agent.RunAsync(reply, session);
    requests = Pending(response);
}

Console.WriteLine(response.Text);
Console.WriteLine($"Shopping list: {string.Join(", ", shoppingList)}");

static List<ToolApprovalRequestContent> Pending(
    AgentResponse response) =>
    response.Messages
        .SelectMany(m => m.Contents)
        .OfType<ToolApprovalRequestContent>()
        .ToList();

static ChatMessage Answer(
    List<ToolApprovalRequestContent> requests,
    bool approved,
    string? reason) =>
    new(ChatRole.User,
        [.. requests.Select(r => r.CreateResponse(approved, reason))]);
