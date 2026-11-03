using Anthropic;
using Anthropic.Core;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Checkpointing;
using Microsoft.Extensions.AI;

var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
    ?? throw new InvalidOperationException("Set ANTHROPIC_API_KEY.");

IChatClient chatClient =
    new AnthropicClient(new ClientOptions { ApiKey = apiKey })
        .AsIChatClient("claude-haiku-5-5", 4096);

AIAgent shopper = chatClient.AsAIAgent(
    name: "shopper",
    instructions: """
        You write the weekly shopping basket for one household.
        One item per line with its quantity, for example
        "500 g chicken thighs". Leave out what the household
        already has. Nothing else.
        """);

Workflow workflow = ShopWorkflow.Build(shopper);

const string SessionId = "weekly-shop";
var folder = new DirectoryInfo(
    Path.Combine(AppContext.BaseDirectory, "checkpoints"));
var store = new FileSystemJsonCheckpointStore(folder);
CheckpointManager checkpoints = CheckpointManager.CreateJson(store);

CheckpointInfo? waiting =
    await checkpoints.GetLatestCheckpointAsync(SessionId);

if (waiting is null)
{
    await using StreamingRun run =
        await InProcessExecution.RunStreamingAsync(
            workflow,
            """
            Dinners this week: chicken stir-fry on Monday, lentil
            soup on Wednesday, homemade pizza on Saturday. We have
            rice, onions and olive oil.
            """,
            checkpoints,
            SessionId);

    await WatchAsync(run, canAnswer: false);
    if (await run.GetStatusAsync() == RunStatus.PendingRequests)
    {
        Console.WriteLine("Waiting for approval. Run the app again to answer.");
    }
    else
    {
        Environment.ExitCode = 1;
    }
    store.Dispose();
}
else
{
    await using StreamingRun run =
        await InProcessExecution.ResumeStreamingAsync(
            workflow, waiting, checkpoints);

    await WatchAsync(run, canAnswer: true);
    store.Dispose();
    folder.Delete(recursive: true);
}

static async Task WatchAsync(StreamingRun run, bool canAnswer)
{
    await foreach (WorkflowEvent evt in
        run.WatchStreamAsync(blockOnPendingRequest: canAnswer))
    {
        switch (evt)
        {
            case ShopProgressEvent progress:
                Console.WriteLine($"[{progress.Step}] {progress.Detail}");
                break;
            case RequestInfoEvent request
                when request.Request.TryGetDataAs(out Basket? basket):
                Console.WriteLine(
                    $"Basket {basket.Round} of {ShopWorkflow.MaxRounds}:");
                foreach (string item in basket.Items)
                {
                    Console.WriteLine($"  {item}");
                }
                if (canAnswer)
                {
                    Console.Write("Approve this basket? (y, n or a change) ");
                    BasketDecision decision = BasketDecision.FromAnswer(
                        Console.ReadLine(), basket.Round);
                    await run.SendResponseAsync(
                        request.Request.CreateResponse(decision));
                }
                break;
            case WorkflowOutputEvent output:
                Console.WriteLine(output.Data);
                break;
            case ExecutorFailedEvent failed:
                Console.Error.WriteLine(
                    $"{failed.ExecutorId} failed: {failed.Data}");
                break;
            case WorkflowErrorEvent error:
                Console.Error.WriteLine(
                    $"Workflow error: {error.Exception}");
                break;
        }
    }
}
