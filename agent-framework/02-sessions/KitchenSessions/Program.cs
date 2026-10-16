using System.Text.Json;
using Anthropic;
using Anthropic.Core;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
    ?? throw new InvalidOperationException("Set ANTHROPIC_API_KEY.");

AIAgent agent =
    new AnthropicClient(new ClientOptions { ApiKey = apiKey })
        .AsIChatClient("claude-haiku-5-5", 1024)
        .AsAIAgent(
            name: "kitchen-assistant",
            instructions:
                "You help one household decide what to cook. "
                + "Answer in at most three sentences.");

AgentSession session = await agent.CreateSessionAsync();

Console.WriteLine(await agent.RunAsync(
    "What can I cook tonight with rice, eggs and an onion?", session));
Console.WriteLine(await agent.RunAsync("And tomorrow?", session));

if (session.TryGetInMemoryChatHistory(out List<ChatMessage>? history))
{
    foreach (ChatMessage message in history)
    {
        Console.WriteLine($"{message.Role}: {message.Text}");
    }
}

string[] questions =
[
    "I also have a leek. Does that change anything?",
    "Which of those keeps best for lunch the next day?",
];

foreach (string question in questions)
{
    AgentResponse response = await agent.RunAsync(question, session);
    Console.WriteLine(
        $"{response.Usage?.InputTokenCount} input tokens: {question}");
}

string path =
    Path.Combine(AppContext.BaseDirectory, "kitchen-session.json");

JsonElement saved = await agent.SerializeSessionAsync(session);
await File.WriteAllTextAsync(path, saved.GetRawText());

// On the next start of the program:
AgentSession restored = await agent.DeserializeSessionAsync(
    JsonElement.Parse(await File.ReadAllTextAsync(path)));

Console.WriteLine(await agent.RunAsync(
    "Remind me what I had in the cupboard.", restored));
