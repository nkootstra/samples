using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

sealed record ScenarioResult(
    string Name,
    int Passed,
    int Runs,
    long InputTokens,
    long OutputTokens,
    List<string> Failures);

static class ScenarioRunner
{
    public static async Task<ScenarioResult> RunAsync(
        IChatClient chatClient,
        Scenario scenario,
        int runs,
        CancellationToken cancellationToken = default)
    {
        int passed = 0;
        long inputTokens = 0, outputTokens = 0;
        List<string> failures = [];

        for (int run = 1; run <= runs; run++)
        {
            var kitchen = new Kitchen();
            AIAgent agent = KitchenAgent.Create(chatClient, kitchen);

            AgentResponse response = await agent.RunAsync(
                scenario.Prompt, cancellationToken: cancellationToken);

            inputTokens += response.Usage?.InputTokenCount ?? 0;
            outputTokens += response.Usage?.OutputTokenCount ?? 0;

            List<FunctionCallContent> calls = response.Messages
                .SelectMany(m => m.Contents)
                .OfType<FunctionCallContent>()
                .ToList();

            string? problem = response.FinishReason == ChatFinishReason.Stop
                ? scenario.Check(new ScenarioRun(calls, kitchen.ShoppingList))
                : $"finish: {response.FinishReason}";

            if (problem is null)
            {
                passed++;
            }
            else
            {
                failures.Add($"run {run}: {problem} " +
                    $"(calls: {Describe(calls)}; " +
                    $"list: {string.Join(", ", kitchen.ShoppingList)})");
            }
        }

        return new(scenario.Name, passed, runs,
            inputTokens, outputTokens, failures);
    }

    static string Describe(List<FunctionCallContent> calls) =>
        string.Join(", ", calls.Select(c =>
            $"{c.Name}({string.Join(", ", c.Arguments?
                .Select(a => $"{a.Key}: {a.Value}") ?? [])})"));
}
