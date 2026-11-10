using System.Globalization;
using Anthropic;
using Anthropic.Core;
using Microsoft.Extensions.AI;

var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
    ?? throw new InvalidOperationException("Set ANTHROPIC_API_KEY.");

IChatClient chatClient =
    new AnthropicClient(new ClientOptions { ApiKey = apiKey })
        .AsIChatClient("claude-haiku-5-5", 4096);

const int Runs = 10;
// Claude Haiku 5.5, US dollars per million tokens.
const decimal InputPrice = 0.10m;
const decimal OutputPrice = 0.50m;

decimal total = 0;
foreach (Scenario scenario in Scenarios.All)
{
    ScenarioResult result =
        await ScenarioRunner.RunAsync(chatClient, scenario, Runs);

    decimal cost = (result.InputTokens * InputPrice
        + result.OutputTokens * OutputPrice) / 1_000_000m;
    total += cost;

    Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
        $"{result.Name,-22} {result.Passed,2}/{result.Runs}  "
        + $"{result.InputTokens,7} in {result.OutputTokens,6} out  "
        + $"${cost / result.Runs:0.00000} per run"));

    foreach (string failure in result.Failures)
    {
        Console.WriteLine($"  {failure}");
    }
}

Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
    $"total ${total:0.0000}"));
