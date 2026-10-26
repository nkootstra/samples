using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using NSubstitute;

namespace KitchenHistory.Tests;

// The instructions and script from Program.cs, and a chat client stub that
// records what every request sends and answers "Answer 1.", "Answer 2.", ...
public abstract class Kitchen
{
    protected const string Instructions =
        "You help one household decide what to cook. "
        + "Answer in at most two sentences.";

    protected static readonly string[] Script =
    [
        "Our son doesn't eat mushrooms. "
            + "What can I cook tonight with rice, eggs and an onion?",
        "And tomorrow?",
        "I also have a leek. Does that change anything?",
        "Which of those keeps best for lunch the next day?",
        "Something quick for Friday?",
        "Remind me: what doesn't our son eat?",
    ];

    protected IChatClient ChatClient { get; } = Substitute.For<IChatClient>();

    // The texts of the messages in each request, in order.
    protected List<string[]> SentTexts { get; } = [];

    int _answers;

    protected Kitchen() =>
        ChatClient
            .GetResponseAsync(
                Arg.Do<IEnumerable<ChatMessage>>(sent =>
                    SentTexts.Add([.. sent.Select(m => m.Text)])),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => new ChatResponse(
                new ChatMessage(ChatRole.Assistant, $"Answer {++_answers}.")));

    protected static IChatClient Summarizer(List<ChatMessage[]>? requests = null)
    {
        IChatClient summarizer = Substitute.For<IChatClient>();
        summarizer
            .GetResponseAsync(
                Arg.Do<IEnumerable<ChatMessage>>(sent => requests?.Add([.. sent])),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Summary.")));
        return summarizer;
    }

    protected static async Task<AgentSession> RunScriptAsync(AIAgent agent, int turns = 6)
    {
        AgentSession session = await agent.CreateSessionAsync(
            TestContext.Current.CancellationToken);
        foreach (string question in Script.Take(turns))
        {
            await agent.RunAsync(
                question, session,
                cancellationToken: TestContext.Current.CancellationToken);
        }

        return session;
    }
}
