using System.ComponentModel;
using System.Globalization;
using Anthropic;
using Anthropic.Core;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

const string SourceName = "Kitchen";

// Aspire sets the endpoint. A plain `dotnet run` doesn't, so the
// spans and metrics go to the console instead.
bool hasCollector = !string.IsNullOrEmpty(
    Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT"));

TracerProviderBuilder tracerBuilder = Sdk.CreateTracerProviderBuilder()
    .AddSource(SourceName)
    .AddSource("Microsoft.Agents.AI.Workflows");
using TracerProvider tracing = hasCollector
    ? tracerBuilder.AddOtlpExporter().Build()
    : tracerBuilder.AddConsoleExporter().Build();

MeterProviderBuilder meterBuilder = Sdk.CreateMeterProviderBuilder()
    .AddMeter(SourceName);
using MeterProvider metrics = hasCollector
    ? meterBuilder.AddOtlpExporter().Build()
    : meterBuilder.AddConsoleExporter().Build();

bool isDevelopment =
    Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") == "Development";

var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
    ?? throw new InvalidOperationException("Set ANTHROPIC_API_KEY.");

IChatClient chatClient =
    new AnthropicClient(new ClientOptions { ApiKey = apiKey })
        .AsIChatClient("claude-haiku-5-5", 4096);

AIAgent Traced(AIAgent agent) => agent
    .AsBuilder()
    .UseOpenTelemetry(
        SourceName,
        configure: otel => otel.EnableSensitiveData = isDevelopment)
    .Build();

AIAgent kitchen = Traced(chatClient.AsAIAgent(
    name: "kitchen-assistant",
    instructions: """
        You help one household decide what to cook.
        Check the pantry before you suggest a dish.
        Answer in at most three sentences.
        """,
    tools: [AIFunctionFactory.Create(ListPantry, name: "list_pantry")]));

AgentResponse answer = await kitchen.RunAsync("What can I cook tonight?");
Console.WriteLine(answer.Text);

// Claude Haiku 5.5, prompts up to 100,000 tokens, in dollars per million
// tokens: platform.claude.com/docs/en/about-claude/pricing, 14 October 2026.
ModelPrice haiku = new(
    InputPerMillion: 0.10m,
    CacheWritePerMillion: 0.125m,
    CacheReadPerMillion: 0.01m,
    OutputPerMillion: 0.50m);

decimal total = 0m;

void Report(string name, AgentResponse response)
{
    decimal cost = haiku.CostOf(response.Usage);
    total += cost;
    Console.WriteLine(
        $"[{name}: {response.Usage?.InputTokenCount} in, "
        + $"{response.Usage?.OutputTokenCount} out, "
        + $"finish: {response.FinishReason}, {Dollars(cost)}]");

    if (response.FinishReason == ChatFinishReason.Length)
    {
        Console.Error.WriteLine($"{name} stopped at the output token limit.");
    }
}

Report("kitchen-assistant", answer);

AIAgent planner = Traced(chatClient.AsAIAgent(
    name: "planner",
    instructions: """
        You plan dinners for one household. Plan three dinners
        for the request. One line per dinner: the day, the dish
        and its main ingredients. Nothing else.
        """));

AIAgent reviewer = Traced(chatClient.AsAIAgent(
    name: "reviewer",
    instructions: """
        You check a dinner plan against the household's rules:
        nobody eats peanuts, fish at most once a week, and every
        weekday dinner takes under 40 minutes. Return the plan
        in the same format with every broken rule fixed, then
        one line per change saying which rule it fixed.
        """));

Workflow workflow = new WorkflowBuilder(planner)
    .AddEdge(planner, reviewer)
    .WithOpenTelemetry(otel => otel.EnableSensitiveData = isDevelopment)
    .Build();

await using StreamingRun run = await InProcessExecution.RunStreamingAsync(
    workflow, "Dinners for Monday, Wednesday and Saturday. We have salmon.");
await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

Dictionary<string, List<AgentResponseUpdate>> updates = [];
await foreach (WorkflowEvent evt in run.WatchStreamAsync())
{
    switch (evt)
    {
        case AgentResponseUpdateEvent update:
            updates.TryAdd(update.ExecutorId, []);
            updates[update.ExecutorId].Add(update.Update);
            break;
        case ExecutorFailedEvent failed:
            Console.Error.WriteLine($"{failed.ExecutorId} failed: {failed.Data}");
            break;
        case WorkflowErrorEvent error:
            Console.Error.WriteLine($"Workflow error: {error.Exception}");
            break;
    }
}

foreach ((string executorId, List<AgentResponseUpdate> agentUpdates) in updates)
{
    Report(executorId.Split('_')[0], agentUpdates.ToAgentResponse());
}

Console.WriteLine($"Total: {Dollars(total)}");

[Description("Lists what is in the household's pantry.")]
static string ListPantry() => "rice, eggs, onions, spinach, feta";

static string Dollars(decimal amount) =>
    amount.ToString("$0.000000", CultureInfo.InvariantCulture);
