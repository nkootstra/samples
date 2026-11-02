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
        You plan dinners for one household. Plan one dinner
        for each day in the request.
        """);

AIAgent peanutChecker = chatClient.AsAIAgent(
    name: "peanut-checker",
    instructions: """
        You check a dinner plan for peanuts, including hidden
        ones such as satay sauce or peanut oil. List each day
        whose dinner probably contains peanuts, with the reason
        in a few words. Return an empty list if none does.
        """);

Workflow workflow = DinnerWorkflow.Build(planner, peanutChecker);

string request = "Dinners for Monday, Wednesday and Saturday. We have salmon.";
Console.WriteLine($"> {request}");

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
            if (invoked.Data is Review review)
            {
                foreach (string problem in review.Problems)
                {
                    Console.Write($"\n  - {problem}");
                }
            }
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
