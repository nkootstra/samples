using System.Globalization;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace KitchenDate.Tests;

// The provider registered as in Program.cs, with a stubbed chat client in
// place of Anthropic and a fake clock in place of TimeProvider.System.
public sealed class TodayContextProviderTests
{
    const string Instructions =
        "You help one household decide what to cook. "
        + "Keep answers short.";

    readonly IChatClient _chatClient = Substitute.For<IChatClient>();
    readonly List<string?> _sentInstructions = [];

    // Wednesday 14 October 2026, 10:00 UTC, in a UTC time zone.
    readonly FakeTimeProvider _clock =
        new(new DateTimeOffset(2026, 10, 14, 10, 0, 0, TimeSpan.Zero));

    public TodayContextProviderTests() =>
        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Do<ChatOptions?>(options => _sentInstructions.Add(options?.Instructions)),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Pasta.")));

    ChatClientAgent KitchenAssistant() =>
        _chatClient.AsAIAgent(new ChatClientAgentOptions
        {
            Name = "kitchen-assistant",
            ChatOptions = new ChatOptions { Instructions = Instructions },
            AIContextProviders = [new TodayContextProvider(_clock)],
        });

    [Fact]
    public async Task The_date_is_added_to_the_instructions_on_its_own_line()
    {
        await KitchenAssistant().RunAsync(
            "Plan dinners for the rest of this week.", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(
            $"{Instructions}\nToday is Wednesday 14 October 2026.",
            Assert.Single(_sentInstructions));
    }

    [Fact]
    public async Task Every_run_reads_the_clock_again()
    {
        AIAgent agent = KitchenAssistant();
        await agent.RunAsync(
            "Dinner tonight?",
            cancellationToken: TestContext.Current.CancellationToken);

        _clock.Advance(TimeSpan.FromDays(1));
        await agent.RunAsync(
            "Dinner tonight?",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Collection(
            _sentInstructions,
            first => Assert.EndsWith("Today is Wednesday 14 October 2026.", first),
            second => Assert.EndsWith("Today is Thursday 15 October 2026.", second));
    }

    [Fact]
    public async Task The_date_is_the_date_in_the_clocks_local_time_zone()
    {
        // 23:30 UTC on Wednesday is already Thursday in Amsterdam.
        _clock.SetUtcNow(new DateTimeOffset(2026, 10, 14, 23, 30, 0, TimeSpan.Zero));
        _clock.SetLocalTimeZone(TimeZoneInfo.FindSystemTimeZoneById("Europe/Amsterdam"));

        await KitchenAssistant().RunAsync(
            "What about tomorrow?", cancellationToken: TestContext.Current.CancellationToken);

        Assert.EndsWith("Today is Thursday 15 October 2026.", Assert.Single(_sentInstructions));
    }

    [Fact]
    public async Task The_date_stays_in_english_on_a_machine_with_a_dutch_culture()
    {
        AIAgent agent = KitchenAssistant();
        CultureInfo culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("nl-NL");
        try
        {
            await agent.RunAsync(
                "Plan dinners for the rest of this week.",
                cancellationToken: TestContext.Current.CancellationToken);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }

        Assert.EndsWith("Today is Wednesday 14 October 2026.", Assert.Single(_sentInstructions));
    }

    [Fact]
    public async Task The_date_stays_out_of_the_session_history()
    {
        AIAgent agent = KitchenAssistant();
        AgentSession session = await agent.CreateSessionAsync(
            TestContext.Current.CancellationToken);

        await agent.RunAsync(
            "Plan dinners for the rest of this week.", session,
            cancellationToken: TestContext.Current.CancellationToken);

        // The model did get the date, in the instructions of this run.
        Assert.EndsWith("Today is Wednesday 14 October 2026.", Assert.Single(_sentInstructions));
        Assert.True(session.TryGetInMemoryChatHistory(out List<ChatMessage>? history));
        string[] expected =
        [
            "user: Plan dinners for the rest of this week.",
            "assistant: Pasta.",
        ];
        Assert.Equal(expected, history.Select(m => $"{m.Role}: {m.Text}").ToArray());
    }
}
