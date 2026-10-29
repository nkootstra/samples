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

AIAgent classifier = chatClient.AsAIAgent(
    name: "classifier",
    instructions: """
        You sort requests to a household's dinner planner.
        QuickDinner: one dinner for tonight. WeekPlan: dinners
        for one or more named days. SomethingElse: anything else.
        """);

AIAgent cook = chatClient.AsAIAgent(
    name: "cook",
    instructions: """
        You suggest one dinner for tonight that takes under
        30 minutes. One line: the dish and its main
        ingredients. Nothing else.
        """);

AIAgent planner = chatClient.AsAIAgent(
    name: "planner",
    instructions: """
        You plan dinners for one household. Plan one dinner
        for each day in the request.
        """);

Workflow workflow = DinnerWorkflow.Build(classifier, cook, planner);

string[] requests =
[
    "Something quick for tonight. We have eggs and spinach.",
    "Dinners for Monday, Wednesday and Saturday. We have salmon.",
    "Which wine goes with lasagne?",
];

foreach (string request in requests)
{
    Console.WriteLine($"\n> {request}");

    await using StreamingRun run =
        await InProcessExecution.RunStreamingAsync(workflow, request);

    await foreach (WorkflowEvent evt in run.WatchStreamAsync())
    {
        switch (evt)
        {
            case SuperStepStartedEvent step:
                Console.Write($"\n[step {step.StepNumber}]");
                break;
            case ExecutorInvokedEvent invoked:
                Console.Write($" {invoked.ExecutorId}");
                break;
            case WorkflowOutputEvent output:
                Console.WriteLine($"\n{output.Data}");
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
}
