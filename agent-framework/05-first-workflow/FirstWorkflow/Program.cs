using Anthropic;
using Anthropic.Core;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
    ?? throw new InvalidOperationException("Set ANTHROPIC_API_KEY.");

IChatClient chatClient =
    new AnthropicClient(new ClientOptions { ApiKey = apiKey })
        .AsIChatClient("claude-haiku-5-5", 4096);

AIAgent planner = chatClient.AsAIAgent(
    name: "planner",
    instructions: """
        You plan dinners for one household. Plan three dinners
        for the request. One line per dinner: the day, the dish
        and its main ingredients. Nothing else.
        """);

AIAgent reviewer = chatClient.AsAIAgent(
    name: "reviewer",
    instructions: """
        You check a dinner plan against the household's rules:
        nobody eats peanuts, fish at most once a week, and every
        weekday dinner takes under 40 minutes. Return the plan
        in the same format with every broken rule fixed, then
        one line per change saying which rule it fixed.
        """);

Workflow workflow = new WorkflowBuilder(planner)
    .AddEdge(planner, reviewer)
    .WithOutputFrom(planner, reviewer)
    .Build();

await using StreamingRun run =
    await InProcessExecution.RunStreamingAsync(
        workflow,
        "Dinners for Monday, Wednesday and Saturday. We have salmon.");

await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

string? lastExecutor = null;
HashSet<string> cutOff = [];
await foreach (WorkflowEvent evt in run.WatchStreamAsync())
{
    switch (evt)
    {
        case AgentResponseUpdateEvent update:
            if (update.ExecutorId != lastExecutor)
            {
                lastExecutor = update.ExecutorId;
                Console.WriteLine($"\n--- {update.ExecutorId}");
            }
            Console.Write(update.Update.Text);
            if (update.Update.FinishReason == ChatFinishReason.Length
                && cutOff.Add(update.ExecutorId))
            {
                Console.Error.WriteLine(
                    $"\n{update.ExecutorId} stopped at the output token limit.");
            }
            break;
        case ExecutorFailedEvent failed:
            Console.Error.WriteLine(
                $"\n{failed.ExecutorId} failed: {failed.Data}");
            break;
        case WorkflowErrorEvent error:
            Console.Error.WriteLine(
                $"\nWorkflow error: {error.Exception}");
            break;
    }
}

Console.WriteLine("\nWorkflow finished.");
