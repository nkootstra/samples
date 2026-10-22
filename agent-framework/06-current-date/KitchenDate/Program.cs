using Anthropic;
using Anthropic.Core;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
    ?? throw new InvalidOperationException("Set ANTHROPIC_API_KEY.");

IChatClient chatClient =
    new AnthropicClient(new ClientOptions { ApiKey = apiKey })
        .AsIChatClient("claude-haiku-5-5", 1024);

const string Instructions =
    "You help one household decide what to cook. "
    + "Keep answers short.";
const string Question =
    "Plan dinners for the rest of this week, one line per day.";

AIAgent agent = chatClient.AsAIAgent(
    name: "kitchen-assistant",
    instructions: Instructions);

Console.WriteLine(await agent.RunAsync(Question));

agent = chatClient.AsAIAgent(new ChatClientAgentOptions
{
    Name = "kitchen-assistant",
    ChatOptions = new ChatOptions { Instructions = Instructions },
    AIContextProviders =
        [new TodayContextProvider(TimeProvider.System)],
});

AgentSession session = await agent.CreateSessionAsync();
Console.WriteLine(await agent.RunAsync(Question, session));

if (session.TryGetInMemoryChatHistory(out List<ChatMessage>? history))
{
    foreach (ChatMessage message in history)
    {
        Console.WriteLine($"{message.Role}: {message.Text}");
    }
}
