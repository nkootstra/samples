# Part 3: Tool calling for AI agents in C#

A kitchen assistant with two tools, plain C# methods on a `PantryTools` class: `find_in_pantry` looks up one ingredient, `list_expiring_soon` lists what has to be used first. The app prints the name, description and JSON schema the model sees for each tool, asks what to cook tonight, and prints every tool call, tool result and text in the response. This is the code from [Tool calling for AI agents in C#](https://kootstra.io/writing/maf-tools/).

## What you need

Install these first (once for all samples), as described in [docs/prerequisites.md](../../docs/prerequisites.md):

- **.NET 10 SDK**
- **An Anthropic API key**
- **Aspire CLI**, optional: only to run the sample through its AppHost (see [With Aspire](#with-aspire-optional))

## Run it

From this folder, set the key for this terminal and run the app. `read -rs` asks for the key without echoing it, so it stays out of your shell history:

```sh
read -rs ANTHROPIC_API_KEY && export ANTHROPIC_API_KEY
dotnet run --project KitchenTools
```

The use-by dates are relative to the day you run it. The shape of the output (answers shortened here):

```text
find_in_pantry: Looks up one ingredient in the household's pantry.
{"type":"object","properties":{"ingredient":{"description":"One ingredient as a plain noun, for example eggs.","type":"string"}},"required":["ingredient"]}
list_expiring_soon: Lists pantry items that expire soon, soonest first.
{"type":"object","properties":{"days":{"description":"How many days ahead to look, from 1 to 14.","type":"integer"}},"required":["days"]}
Tonight, cook something that uses the 200 g of spinach (expires tomorrow, October 18) [...]
call   list_expiring_soon(days: 3)
result Today is 2026-10-17. Expiring: spinach (200 g, use by 2026-10-18); feta (150 g, use by 2026-10-20).
text   Tonight, cook something that uses the 200 g of spinach [...]
```

### With Aspire (optional)

This sample has an AppHost that starts the app and passes it the key from user secrets, stored as described in [docs/prerequisites.md](../../docs/prerequisites.md). From this folder:

```sh
aspire run
```

Open the dashboard link that `aspire run` prints and go to **Console logs**, resource `kitchen-tools`. The app runs once and exits; stop the AppHost with `Ctrl+C`. To read the output in the terminal instead, start the AppHost in the background and wait for the app to exit before reading its logs, or you may get only part of them:

```sh
aspire start
aspire wait kitchen-tools --status down
aspire logs kitchen-tools
aspire stop
```

## Run the tests

```sh
dotnet test
```

Fourteen tests, no API calls:

- `PantryToolsTests` call the pantry methods directly: an ingredient comes back with today's date, its amount and its `yyyy-MM-dd` use-by date (also in a culture with another calendar, such as Thai), the lookup ignores case, a missing ingredient lists what the pantry has, expiring items start with today's date and come soonest first, and a day count outside 1 to 14 is answered with what to do instead.
- `ToolTests` check what the model sees (name, description and the schema built from the parameter types) and, with a stubbed chat client, that the agent runs the requested tool and sends its result back, and that a thrown exception reaches the model as `Error: Function failed.`.
- `AppHostTests` check that the AppHost passes the secret `anthropic-api-key` parameter to the app as `ANTHROPIC_API_KEY`.

`PantryTools` has no access modifier, so it is `internal`; `KitchenTools.csproj` makes it visible to the test project with `InternalsVisibleTo`.

## Files

| File | What it is |
| --- | --- |
| `KitchenTools/Program.cs` | Turns the methods into tools, prints their schemas, runs the agent and prints the calls |
| `KitchenTools/PantryTools.cs` | The pantry and its two tool methods |
| `KitchenTools.AppHost/AppHost.cs` | Starts the app with the API key |
| `KitchenTools.Tests/` | Tests for the pantry methods and the tool loop |

## Try it

- Remove the `[Description]` from a parameter in `PantryTools.cs`, run again and compare the printed schema. That description is all the model knows about the parameter.
- Make `FindInPantry` throw an exception and look at the `result` line. The model receives `Error: Function failed.`, not your exception; the `threw` line shows what your code can still read.
