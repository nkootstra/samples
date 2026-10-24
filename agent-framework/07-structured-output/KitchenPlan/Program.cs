using System.Text.Json;
using Anthropic;
using Anthropic.Core;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
    ?? throw new InvalidOperationException("Set ANTHROPIC_API_KEY.");

IChatClient chatClient =
    new AnthropicClient(new ClientOptions { ApiKey = apiKey })
        .AsIChatClient("claude-haiku-5-5", 4096);

ChatResponseFormatJson format = ChatResponseFormat.ForJsonSchema<DinnerPlan>(
    AgentAbstractionsJsonUtilities.DefaultOptions);
Console.WriteLine(format.Schema);

AIAgent planner = chatClient.AsAIAgent(
    name: "planner",
    instructions: """
        You plan dinners for one household. Plan one dinner
        for each day in the request.
        """);

AgentSession session = await planner.CreateSessionAsync();
AgentResponse<DinnerPlan> response = await planner.RunAsync<DinnerPlan>(
    "Dinners for Monday, Wednesday and Saturday. We have salmon.",
    session);

for (int attempt = 1; ; attempt++)
{
    Console.WriteLine(
        $"[{response.Usage?.InputTokenCount} in, "
        + $"{response.Usage?.OutputTokenCount} out, "
        + $"finish: {response.FinishReason}]");

    if (response.FinishReason != ChatFinishReason.Stop)
    {
        Console.WriteLine("No complete plan, so it is not read.");
        break;
    }

    DinnerPlan plan;
    try
    {
        plan = response.Result;
    }
    catch (Exception ex)
        when (ex is JsonException or InvalidOperationException)
    {
        Console.WriteLine($"Not a dinner plan: {ex.Message}");
        break;
    }

    foreach (Dinner dinner in plan.Dinners)
    {
        Console.WriteLine(
            $"{dinner.Day}: {dinner.Dish}, {dinner.Minutes} min "
            + $"({string.Join(", ", dinner.MainIngredients)})");
    }

    List<string> problems = HouseholdRules.Check(plan);
    if (problems.Count == 0)
    {
        Console.WriteLine("The plan keeps every rule.");
        break;
    }

    foreach (string problem in problems)
    {
        Console.WriteLine($"- {problem}");
    }

    if (attempt == 3)
    {
        Console.WriteLine("Still breaking the rules after three plans.");
        break;
    }

    response = await planner.RunAsync<DinnerPlan>(
        "Fix these problems and return the whole plan:\n"
            + string.Join("\n", problems),
        session);
}
