using Anthropic;
using Anthropic.Core;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.DevUI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

var apiKey = builder.Configuration["ANTHROPIC_API_KEY"]
    ?? throw new InvalidOperationException("Set ANTHROPIC_API_KEY.");

builder.Services.AddChatClient(
    new AnthropicClient(new ClientOptions { ApiKey = apiKey })
        .AsIChatClient("claude-haiku-5-5", 4096));

var pantry = new PantryTools();

builder.AddAIAgent("kitchen-assistant", """
    You help one household decide what to cook.
    Check the pantry with your tools before suggesting a dish.
    Prefer ingredients that expire soon. Answer in at most
    three sentences.
    """)
    .WithAITools(
        AIFunctionFactory.Create(
            pantry.FindInPantry, name: "find_in_pantry"),
        AIFunctionFactory.Create(
            pantry.ListExpiringSoon, name: "list_expiring_soon"));

var planner = builder.AddAIAgent("planner", """
    You plan dinners for one household. Plan three dinners
    for the request. One line per dinner: the day, the dish
    and its main ingredients. Nothing else.
    """);

var reviewer = builder.AddAIAgent("reviewer", """
    You check a dinner plan against the household's rules:
    nobody eats peanuts, fish at most once a week, and every
    weekday dinner takes under 40 minutes. Return the plan
    in the same format with every broken rule fixed, then
    one line per change saying which rule it fixed.
    """);

builder.AddWorkflow("dinner-review", (sp, key) =>
    AgentWorkflowBuilder.BuildSequential(
        key,
        sp.GetRequiredKeyedService<AIAgent>(planner.Name),
        sp.GetRequiredKeyedService<AIAgent>(reviewer.Name)));

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddOpenAIResponses();
    builder.Services.AddOpenAIConversations();
    builder.AddDevUI();
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenAIResponses();
    app.MapOpenAIConversations();
    app.MapDevUI();
}

app.Run();
