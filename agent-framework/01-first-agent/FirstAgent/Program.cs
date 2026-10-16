using Anthropic;
using Anthropic.Core;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
    ?? throw new InvalidOperationException("Set ANTHROPIC_API_KEY.");

IChatClient chatClient =
    new AnthropicClient(new ClientOptions { ApiKey = apiKey })
        .AsIChatClient("claude-haiku-5-5", 1024);

AIAgent agent = chatClient.AsAIAgent(
    name: "kitchen-assistant",
    instructions: """
        You help one household decide what to cook.
        Answer in at most three sentences.
        """);

AgentResponse response = await agent.RunAsync(
    "What can I cook tonight with rice, eggs and an onion?");

Console.WriteLine(response.Text);

Console.WriteLine(
    $"[{response.Usage?.InputTokenCount} in, "
    + $"{response.Usage?.OutputTokenCount} out, "
    + $"finish: {response.FinishReason}]");

if (response.FinishReason == ChatFinishReason.Length)
{
    Console.WriteLine("The answer stopped at the output token limit.");
}

List<AgentResponseUpdate> updates = [];

await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(
    "Suggest one dessert that uses eggs."))
{
    Console.Write(update.Text);
    updates.Add(update);
}
Console.WriteLine();

AgentResponse streamed = updates.ToAgentResponse();
Console.WriteLine($"[{streamed.Usage?.OutputTokenCount} out]");
