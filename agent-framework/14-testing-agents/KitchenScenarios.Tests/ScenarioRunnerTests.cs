using Microsoft.Extensions.AI;
using NSubstitute;

namespace KitchenScenarios.Tests;

// The runner is code, so it gets ordinary tests with a scripted model.
public sealed class ScenarioRunnerTests
{
    readonly IChatClient _chatClient = Substitute.For<IChatClient>();

    // The model's responses, in order, across every run.
    void ModelResponds(params ChatResponse[] responses)
    {
        var queue = new Queue<ChatResponse>(responses);
        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => queue.Dequeue());
    }

    static ChatResponse Adds(string item, int inputTokens = 0, int outputTokens = 0) =>
        new(new ChatMessage(
            ChatRole.Assistant,
            [
                new FunctionCallContent(
                    $"call-{item}",
                    "add_to_shopping_list",
                    new Dictionary<string, object?> { ["item"] = item }),
            ]))
        {
            FinishReason = ChatFinishReason.ToolCalls,
            Usage = new() { InputTokenCount = inputTokens, OutputTokenCount = outputTokens },
        };

    static ChatResponse Answers(
        ChatFinishReason finishReason, int inputTokens = 0, int outputTokens = 0) =>
        new(new ChatMessage(ChatRole.Assistant, "Done."))
        {
            FinishReason = finishReason,
            Usage = new() { InputTokenCount = inputTokens, OutputTokenCount = outputTokens },
        };

    static Scenario OneItemOnTheList(Func<ScenarioRun, string?>? check = null) =>
        new(
            "one item",
            "We're out of milk.",
            check ?? (run => run.ShoppingList.Count == 1 ? null : "not one item"));

    Task<ScenarioResult> RunAsync(Scenario scenario, int runs) =>
        ScenarioRunner.RunAsync(
            _chatClient, scenario, runs, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Every_run_that_passes_the_check_is_counted()
    {
        ModelResponds(
            Adds("milk"), Answers(ChatFinishReason.Stop),
            Adds("milk"), Answers(ChatFinishReason.Stop),
            Adds("milk"), Answers(ChatFinishReason.Stop));

        ScenarioResult result = await RunAsync(OneItemOnTheList(), runs: 3);

        Assert.Equal((3, 3), (result.Passed, result.Runs));
        Assert.Empty(result.Failures);
    }

    [Fact]
    public async Task Each_run_starts_with_an_empty_shopping_list()
    {
        ModelResponds(
            Adds("milk"), Answers(ChatFinishReason.Stop),
            Adds("eggs"), Answers(ChatFinishReason.Stop));

        ScenarioResult result = await RunAsync(OneItemOnTheList(), runs: 2);

        Assert.Equal(2, result.Passed);
    }

    [Fact]
    public async Task A_failed_run_records_the_reason_the_calls_and_the_list()
    {
        ModelResponds(Adds("milk"), Answers(ChatFinishReason.Stop));
        Scenario scenario = OneItemOnTheList(_ => "wrong item");

        ScenarioResult result = await RunAsync(scenario, runs: 1);

        Assert.Equal(0, result.Passed);
        Assert.Equal(
            ["run 1: wrong item (calls: add_to_shopping_list(item: milk); list: milk)"],
            result.Failures);
    }

    [Fact]
    public async Task A_run_that_did_not_finish_fails_without_running_the_check()
    {
        ModelResponds(Adds("milk"), Answers(ChatFinishReason.Length));
        Scenario scenario = OneItemOnTheList(_ => null);

        ScenarioResult result = await RunAsync(scenario, runs: 1);

        Assert.Equal(0, result.Passed);
        Assert.StartsWith("run 1: finish: length ", result.Failures[0]);
    }

    [Fact]
    public async Task The_check_sees_every_tool_call_the_model_made()
    {
        ModelResponds(Adds("milk"), Answers(ChatFinishReason.Stop));
        List<string> seen = [];
        Scenario scenario = OneItemOnTheList(run =>
        {
            seen.AddRange(run.Calls.Select(c => c.Name));
            return null;
        });

        await RunAsync(scenario, runs: 1);

        Assert.Equal(["add_to_shopping_list"], seen);
    }

    [Fact]
    public async Task Tokens_add_up_over_every_model_call_and_every_run()
    {
        ModelResponds(
            Adds("milk", 100, 10), Answers(ChatFinishReason.Stop, 200, 20),
            Adds("milk", 100, 10), Answers(ChatFinishReason.Stop, 200, 20));

        ScenarioResult result = await RunAsync(OneItemOnTheList(), runs: 2);

        Assert.Equal((600, 60), (result.InputTokens, result.OutputTokens));
    }
}
