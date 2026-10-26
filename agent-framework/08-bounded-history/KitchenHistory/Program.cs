using Anthropic;
using Anthropic.Core;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;

var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
    ?? throw new InvalidOperationException("Set ANTHROPIC_API_KEY.");

IChatClient chatClient =
    new AnthropicClient(new ClientOptions { ApiKey = apiKey })
        .AsIChatClient("claude-haiku-5-5", 1024);

const string Instructions =
    "You help one household decide what to cook. "
    + "Answer in at most two sentences.";

string[] script =
[
    "Our son doesn't eat mushrooms. "
        + "What can I cook tonight with rice, eggs and an onion?",
    "And tomorrow?",
    "I also have a leek. Does that change anything?",
    "Which of those keeps best for lunch the next day?",
    "Something quick for Friday?",
    "Remind me: what doesn't our son eat?",
];

async Task RunScriptAsync(AIAgent agent)
{
    AgentSession session = await agent.CreateSessionAsync();
    AgentResponse? response = null;

    foreach (string question in script)
    {
        response = await agent.RunAsync(question, session);
        Console.WriteLine(
            $"{response.Usage?.InputTokenCount,4} input tokens, "
            + $"{response.FinishReason}: {question}");
    }

    Console.WriteLine(response?.Text);

    if (session.TryGetInMemoryChatHistory(out List<ChatMessage>? history))
    {
        Console.WriteLine(
            $"{history.Count} messages stored, the first: {history[0].Text}");
    }
}

AIAgent agent = chatClient.AsAIAgent(
    name: "kitchen-assistant",
    instructions: Instructions);

await RunScriptAsync(agent);

agent = chatClient.AsAIAgent(new ChatClientAgentOptions
{
    Name = "kitchen-assistant",
    ChatOptions = new ChatOptions { Instructions = Instructions },
    AIContextProviders =
    [
        new CompactionProvider(new SlidingWindowCompactionStrategy(
            CompactionTriggers.TurnsExceed(3))),
    ],
});

await RunScriptAsync(agent);

agent = chatClient.AsAIAgent(new ChatClientAgentOptions
{
    Name = "kitchen-assistant",
    ChatOptions = new ChatOptions { Instructions = Instructions },
    AIContextProviders =
    [
        new CompactionProvider(new ContextWindowCompactionStrategy(
            maxContextWindowTokens: 1324,
            maxOutputTokens: 1024)),
    ],
});

await RunScriptAsync(agent);

async Task<ChatResponse> PrintSummaryUsageAsync(
    IEnumerable<ChatMessage> messages,
    ChatOptions? options,
    IChatClient inner,
    CancellationToken cancellationToken)
{
    ChatResponse summary = await inner.GetResponseAsync(
        messages, options, cancellationToken);
    Console.WriteLine(
        $"     summary: {summary.Usage?.InputTokenCount} input, "
        + $"{summary.Usage?.OutputTokenCount} output tokens, "
        + $"{summary.FinishReason}");
    return summary;
}

IChatClient summarizer = chatClient
    .AsBuilder()
    .Use(PrintSummaryUsageAsync, getStreamingResponseFunc: null)
    .Build();

agent = chatClient.AsAIAgent(new ChatClientAgentOptions
{
    Name = "kitchen-assistant",
    ChatOptions = new ChatOptions { Instructions = Instructions },
    AIContextProviders =
    [
        new CompactionProvider(new SummarizationCompactionStrategy(
            new SystemAsUserChatClient(summarizer),
            CompactionTriggers.TurnsExceed(3),
            minimumPreservedGroups: 2)),
    ],
});

await RunScriptAsync(agent);

agent = chatClient.AsAIAgent(new ChatClientAgentOptions
{
    Name = "kitchen-assistant",
    ChatOptions = new ChatOptions { Instructions = Instructions },
    ChatHistoryProvider = new InMemoryChatHistoryProvider(new()
    {
        ChatReducer = new MessageCountingChatReducer(4),
    }),
});

await RunScriptAsync(agent);

agent = chatClient.AsAIAgent(new ChatClientAgentOptions
{
    Name = "kitchen-assistant",
    ChatOptions = new ChatOptions { Instructions = Instructions },
    ChatHistoryProvider = new InMemoryChatHistoryProvider(new()
    {
        ChatReducer = new SummarizingChatReducer(
            new SystemAsUserChatClient(summarizer), 4, 2),
    }),
});

await RunScriptAsync(agent);
