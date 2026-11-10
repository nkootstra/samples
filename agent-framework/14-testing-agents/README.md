# Part 14: Agent Framework tests and pass rates in C# with Claude

A kitchen assistant with a pantry to read (`find_in_pantry`) and a shopping list to write (`add_to_shopping_list`), tested in three layers: unit tests of the `Kitchen` class, tests of the agent wiring with a scripted model, and four real scenarios against Claude Haiku 5.5 that each run ten times. The app is the scenario runner: it prints a pass rate, token counts and cost per scenario, and every failed run with its tool calls and shopping list. This is the code from [Agent Framework tests and pass rates in C# with Claude](https://kootstra.io/writing/agent-framework-testing-agents-csharp/).

## What you need

Install these first (once for all samples), as described in [docs/prerequisites.md](../../docs/prerequisites.md):

- **.NET 10 SDK**
- **Aspire CLI**
- **An Anthropic API key**, stored in user secrets as `Parameters:anthropic-api-key` under the ID `kootstra-samples`

## Run it

One run of the app is 40 agent runs (four scenarios, ten times each), each with one or more requests to Claude Haiku 5.5. It cost about one US cent and took about two minutes when the table below was made.

From this folder:

```sh
aspire run
```

Open the dashboard link that `aspire run` prints and go to **Console logs**, resource `kitchen-scenarios`. The app runs once and exits; stop the AppHost with `Ctrl+C`. To read the output in the terminal instead, start the AppHost in the background and wait for the app to exit before reading its logs, or you may get only part of them:

```sh
aspire start
aspire wait kitchen-scenarios --status down
aspire logs kitchen-scenarios
aspire stop
```

The output of the run in the article. Your pass rates, token counts and costs will differ, and a failed run is printed under its scenario:

```text
milk on the list       10/10    12810 in    679 out  $0.00016 per run
question only          10/10    12590 in    760 out  $0.00016 per run
no peanuts             10/10    13951 in   2957 out  $0.00029 per run
missing for a recipe   10/10    26722 in   3283 out  $0.00043 per run
total $0.0104
```

The prices in `Program.cs` are Claude Haiku 5.5's list prices when this was written ($0.10 input, $0.50 output per million tokens). Check [Anthropic's pricing](https://platform.claude.com/docs/en/about-claude/pricing) before you trust the cost column.

### Without Aspire

```sh
read -rs ANTHROPIC_API_KEY && export ANTHROPIC_API_KEY
dotnet run --project KitchenScenarios
```

## Run the tests

```sh
dotnet test
```

Forty-three tests, no API calls. The scenarios against Claude are not among them: they cost money and a pass rate below 100% would fail the build at random, so they run as the app instead.

- `KitchenTests` (layer 1) call the `Kitchen` methods directly: a pantry lookup returns the amount and ignores case, a missing ingredient lists what the pantry has, an added item is stored trimmed and in lower case, an item is never added twice, and peanuts and empty items are refused with the reason.
- `KitchenAgentTests` (layer 2) run the real agent from `KitchenAgent.Create` against an NSubstitute stub of `IChatClient`: the model gets both tools and the instructions, an add call from the model lands on the kitchen's list, the tool result goes back to the model, and a peanut item stays off the list whatever the model asks.
- `ScenarioCheckTests` check the scenario checks themselves: which lists and calls pass and which fail, including the common pasta shapes counting as pasta, and whole-word, case-insensitive matching (eggplant is not eggs, buttermilk is not milk).
- `ScenarioRunnerTests` run the runner with a scripted model: passes are counted, each run starts with an empty list, a failure records the reason, calls and list, a run that stops at the token limit fails without running the check, and tokens add up over every model call and every run.
- `AppHostTests` check that the AppHost passes the secret `anthropic-api-key` parameter to the app as `ANTHROPIC_API_KEY`.

The app's types are `internal`, as in the article; `KitchenScenarios.csproj` makes them visible to the test project with `InternalsVisibleTo`.

## Files

| File | What it is |
| --- | --- |
| `KitchenScenarios/Program.cs` | Runs every scenario ten times against Claude Haiku 5.5 and prints the table |
| `KitchenScenarios/Kitchen.cs` | The pantry, the shopping list and the two tool methods |
| `KitchenScenarios/KitchenAgent.cs` | Builds the agent with its instructions and tools, for the app and the tests |
| `KitchenScenarios/Scenario.cs` | A scenario (request and check) and what a check sees of a run |
| `KitchenScenarios/Scenarios.cs` | The four scenarios |
| `KitchenScenarios/ScenarioRunner.cs` | Runs one scenario a number of times and counts passes, tokens and failures |
| `KitchenScenarios.AppHost/AppHost.cs` | Starts the app with the API key |
| `KitchenScenarios.Tests/` | The layer 1 and layer 2 tests, and the tests for the checks and the runner |

The app files are the article's code. The article shows only excerpts of the test files.

## Try it

- Remove the line "Only add items to the shopping list when the household asks for it." from the instructions in `KitchenAgent.cs`, run the scenarios again and compare the "question only" row.
- Change `Pasta` in `Scenarios.cs` to `["pasta"]`, run `dotnet test`, then the scenarios. A new failure in "missing for a recipe" is a failure of the check, not of the model.
