using System.Diagnostics;
using System.Globalization;
using Anthropic;
using Anthropic.Core;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Quality;
using Microsoft.Extensions.AI.Evaluation.Reporting;
using Microsoft.Extensions.AI.Evaluation.Reporting.Storage;

var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
    ?? throw new InvalidOperationException("Set ANTHROPIC_API_KEY.");

IChatClient claude =
    new AnthropicClient(new ClientOptions { ApiKey = apiKey })
        .AsIChatClient("claude-haiku-5-5", 4096);

var agentTokens = new TokenCounter(claude);
var judgeTokens = new TokenCounter(claude);
AIAgent agent = KitchenAgent.Create(agentTokens);
ChatConfiguration judge = Judge.Create(judgeTokens);

string[] questions =
[
    "What can I cook tonight?",
    "I have 20 minutes. What's a quick vegetarian dinner?",
    "Can I make lasagne tonight?",
];

// 1. Checks in code, three runs per question.
AgentEvaluationResults checks = await agent.EvaluateAsync(
    questions, Checks.Create(), numRepetitions: 3);
Console.WriteLine($"Checks in code: {checks.Passed}/{checks.Total} passed");
Console.WriteLine();

// 2. The judge on answers written by hand, three times each.
ReportingConfiguration reporting = DiskBasedReportingConfiguration.Create(
    storageRootPath: Path.Combine(Path.GetTempPath(), "kitchen-evals"),
    evaluators: [new RelevanceEvaluator(), new GroundednessEvaluator()],
    chatConfiguration: judge,
    enableResponseCaching: true,
    evaluationMetricInterpreter: Judge.FailWhenErrored);

(string Name, string Answer)[] written =
[
    ("from the pantry",
        "Make a spinach and feta omelette with the eggs and an onion, "
        + "and serve it with rice."),
    ("salmon",
        "Make salmon teriyaki with noodles: marinate the salmon in soy "
        + "sauce and honey, roast it for 12 minutes and serve it over "
        + "the noodles."),
    ("only the pantry",
        "Your pantry has rice, eggs, onions, spinach, feta and two tins "
        + "of tomatoes."),
];

Console.WriteLine("Answers written by hand    relevance  groundedness");
var stopwatch = Stopwatch.StartNew();
foreach (var (name, answer) in written)
{
    List<EvaluationResult> results = [];
    for (int iteration = 1; iteration <= 3; iteration++)
    {
        await using ScenarioRun run = await reporting.CreateScenarioRunAsync(
            name, iterationName: $"{iteration}");
        results.Add(await run.EvaluateAsync(
            new ChatMessage(ChatRole.User, "What can I cook tonight?"),
            new ChatMessage(ChatRole.Assistant, answer),
            [new GroundednessEvaluatorContext(Pantry.Contents)]));
    }

    Console.WriteLine(Report.Row(name, results));
}

Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
    $"  {stopwatch.Elapsed.TotalSeconds:0.0} s, judge tokens: "
    + $"{judgeTokens.InputTokens} in, {judgeTokens.OutputTokens} out"));
Console.WriteLine();

// 3. The judge on the agent's answers, three runs per question.
long judgeIn = judgeTokens.InputTokens, judgeOut = judgeTokens.OutputTokens;
long agentIn = agentTokens.InputTokens, agentOut = agentTokens.OutputTokens;

var pantryJudge = new PantryJudge(
    new CompositeEvaluator(
        new RelevanceEvaluator(), new GroundednessEvaluator()),
    judge);
AgentEvaluationResults judged = await agent.EvaluateAsync(
    questions, pantryJudge, numRepetitions: 3);

Console.WriteLine("The agent's answers        relevance  groundedness");
foreach (string question in questions)
{
    int[] runs = Enumerable.Range(0, judged.Total)
        .Where(i => judged.InputItems![i].Query == question)
        .ToArray();
    EvaluationResult[] results = [.. runs.Select(i => judged.Items[i])];

    Console.WriteLine(question);
    Console.WriteLine(Report.Row("", results));

    foreach (int i in runs.Where(i => Report.Reasons(judged.Items[i]).Any()))
    {
        Console.WriteLine($"  answer: {judged.InputItems![i].Response}");
        foreach (string reason in Report.Reasons(judged.Items[i]))
        {
            Console.WriteLine($"  {reason}");
        }
    }
}

Console.WriteLine($"Passed: {judged.Passed}/{judged.Total}");

// Claude Haiku 5.5, US dollars per million tokens.
const decimal InputPrice = 0.10m;
const decimal OutputPrice = 0.50m;
static decimal Cost(long input, long output) =>
    (input * InputPrice + output * OutputPrice) / 1_000_000m;

long agentInput = agentTokens.InputTokens - agentIn;
long agentOutput = agentTokens.OutputTokens - agentOut;
long judgeInput = judgeTokens.InputTokens - judgeIn;
long judgeOutput = judgeTokens.OutputTokens - judgeOut;
Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
    $"Agent: {agentInput} in, {agentOutput} out, "
    + $"${Cost(agentInput, agentOutput):0.0000}"));
Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
    $"Judge: {judgeInput} in, {judgeOutput} out, "
    + $"${Cost(judgeInput, judgeOutput):0.0000}"));
