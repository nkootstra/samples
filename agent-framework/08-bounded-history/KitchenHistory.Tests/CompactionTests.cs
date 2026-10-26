using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace KitchenHistory.Tests;

// The compaction strategies from Program.cs, with a stubbed chat client in place of Anthropic.
public sealed class CompactionTests : Kitchen
{
    ChatClientAgent WithCompaction(CompactionStrategy strategy) =>
        ChatClient.AsAIAgent(new ChatClientAgentOptions
        {
            Name = "kitchen-assistant",
            ChatOptions = new ChatOptions { Instructions = Instructions },
            AIContextProviders = [new CompactionProvider(strategy)],
        });

    static SlidingWindowCompactionStrategy SlidingWindow() =>
        new(CompactionTriggers.TurnsExceed(3));

    static SummarizationCompactionStrategy Summarization(IChatClient summarizer) =>
        new(summarizer, CompactionTriggers.TurnsExceed(3), minimumPreservedGroups: 2);

    [Fact]
    public async Task A_sliding_window_sends_the_last_two_turns_and_the_new_question()
    {
        await RunScriptAsync(WithCompaction(SlidingWindow()));

        string[] expected =
        [
            Script[3], "Answer 4.", Script[4], "Answer 5.", Script[5],
        ];
        Assert.Equal(expected, SentTexts[^1]);
    }

    [Fact]
    public async Task A_sliding_window_first_cuts_on_the_fourth_turn()
    {
        await RunScriptAsync(WithCompaction(SlidingWindow()), turns: 4);

        Assert.Equal([5, 5], SentTexts[2..].Select(texts => texts.Length));
    }

    [Fact]
    public async Task A_sliding_window_always_keeps_the_latest_turn()
    {
        await RunScriptAsync(WithCompaction(
            new SlidingWindowCompactionStrategy(CompactionTriggers.TurnsExceed(0))));

        Assert.All(SentTexts, (texts, turn) => Assert.Equal([Script[turn]], texts));
    }

    [Fact]
    public async Task A_compaction_provider_leaves_the_stored_history_whole()
    {
        AgentSession session = await RunScriptAsync(WithCompaction(SlidingWindow()));

        Assert.True(session.TryGetInMemoryChatHistory(out List<ChatMessage>? history));
        Assert.Equal(12, history.Count);
        Assert.Equal(Script[0], history[0].Text);
    }

    [Fact]
    public async Task A_saved_session_holds_every_message_twice()
    {
        ChatClientAgent agent = WithCompaction(SlidingWindow());
        AgentSession session = await RunScriptAsync(agent);

        string saved = (await agent.SerializeSessionAsync(
            session, cancellationToken: TestContext.Current.CancellationToken)).GetRawText();

        // Once in the history provider's state, once in the compaction provider's.
        Assert.Equal(2, saved.Split("What can I cook tonight").Length - 1);
        Assert.Equal(2, saved.Split("Answer 1.").Length - 1);
    }

    [Fact]
    public async Task A_token_budget_drops_whole_messages_and_can_start_with_an_answer()
    {
        // An input budget of 50 estimated tokens: truncation starts above 40.
        await RunScriptAsync(
            WithCompaction(new ContextWindowCompactionStrategy(
                maxContextWindowTokens: 1074, maxOutputTokens: 1024)),
            turns: 4);

        // The first question is about 21 tokens: dropping it alone fits the budget.
        string[] expected =
        [
            "Answer 1.", Script[1], "Answer 2.", Script[2], "Answer 3.", Script[3],
        ];
        Assert.Equal(expected, SentTexts[^1]);
    }

    [Fact]
    public async Task Tool_result_compaction_collapses_an_old_tool_call_into_one_message()
    {
        ChatMessage[] messages =
        [
            new(ChatRole.User, "What's in the pantry?"),
            new(ChatRole.Assistant, [new FunctionCallContent("call-1", "get_pantry")]),
            new(ChatRole.Tool, [new FunctionResultContent("call-1", "rice, eggs")]),
            new(ChatRole.Assistant, "Rice and eggs."),
            new(ChatRole.User, "And tomorrow?"),
        ];

        IEnumerable<ChatMessage> compacted = await CompactionProvider.CompactAsync(
            new ToolResultCompactionStrategy(
                CompactionTriggers.HasToolCalls(), minimumPreservedGroups: 2),
            messages,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                "What's in the pantry?",
                "[Tool Calls]\nget_pantry:\n  - rice, eggs",
                "Rice and eggs.",
                "And tomorrow?",
            ],
            compacted.Select(m => m.Text));
    }

    [Fact]
    public async Task A_summarization_strategy_sends_its_prompt_first_and_the_conversation_last()
    {
        List<ChatMessage[]> summaryRequests = [];

        await RunScriptAsync(
            WithCompaction(Summarization(Summarizer(summaryRequests))), turns: 4);

        // The request ends with an assistant message, which Claude rejects.
        ChatMessage[] request = Assert.Single(summaryRequests);
        Assert.Equal(
            [ChatRole.System, ChatRole.User, ChatRole.Assistant],
            request.Select(m => m.Role));
        Assert.Equal(
            SummarizationCompactionStrategy.DefaultSummarizationPrompt, request[0].Text);
    }

    [Fact]
    public async Task A_summarization_strategy_sends_the_summary_in_place_of_the_oldest_turns()
    {
        await RunScriptAsync(WithCompaction(Summarization(Summarizer())));

        string[] expected =
        [
            "[Summary]\nSummary.", Script[3], "Answer 4.", Script[4], "Answer 5.", Script[5],
        ];
        Assert.Equal(expected, SentTexts[^1]);
    }

    [Fact]
    public async Task A_summarization_strategy_summarizes_again_from_the_last_summary()
    {
        List<ChatMessage[]> summaryRequests = [];

        await RunScriptAsync(WithCompaction(Summarization(Summarizer(summaryRequests))));

        Assert.Equal(3, summaryRequests.Count);
        Assert.Equal(
            ["[Summary]\nSummary.", Script[2], "Answer 3."],
            summaryRequests[^1][1..].Select(m => m.Text));
    }

    [Fact]
    public async Task A_summarization_strategy_keeps_the_summary_out_of_the_stored_history()
    {
        AgentSession session = await RunScriptAsync(
            WithCompaction(Summarization(Summarizer())));

        Assert.True(session.TryGetInMemoryChatHistory(out List<ChatMessage>? history));
        Assert.Equal(12, history.Count);
        Assert.DoesNotContain(history, m => m.Text.Contains("Summary", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_failed_summary_sends_the_whole_history_without_an_error()
    {
        IChatClient summarizer = Substitute.For<IChatClient>();
        summarizer
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Bad request"));

        await RunScriptAsync(WithCompaction(Summarization(summarizer)));

        Assert.Equal(11, SentTexts[^1].Length);
    }
}
