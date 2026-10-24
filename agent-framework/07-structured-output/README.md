# Part 7: Agent Framework structured output in C# with Claude

A planner agent asked for three dinners with `RunAsync<DinnerPlan>`, so its answer comes back as a C# record instead of prose. The app prints the JSON schema generated from the record, checks the finish reason before it reads the plan, and runs the plan through `HouseholdRules`, a plain C# check of the household's rules (no peanuts, fish at most once a week, weekday dinners under 40 minutes). Any problems go back to the model in the same session, up to three plans in total. This is the code from [Agent Framework structured output in C# with Claude](https://kootstra.io/writing/structured-output-ai-agent-csharp/).

## What you need

Install these first (once for all samples), as described in [docs/prerequisites.md](../../docs/prerequisites.md):

- **.NET 10 SDK**
- **An Anthropic API key**
- **Aspire CLI**, optional: only to run the sample through its AppHost (see [With Aspire](#with-aspire-optional))

## Run it

From this folder, set the key for this terminal and run the app. `read -rs` asks for the key without echoing it, so it stays out of your shell history:

```sh
read -rs ANTHROPIC_API_KEY && export ANTHROPIC_API_KEY
dotnet run --project KitchenPlan
```

The first line is the schema. After it, each plan comes with its token counts and finish reason, then the problems the check found. A run where the first plan broke the fish rule:

```text
[634 in, 216 out, finish: stop]
Monday: Honey-glazed salmon with roasted vegetables, 35 min (salmon, carrot, zucchini, honey)
Wednesday: Salmon and vegetable stir-fry with rice, 25 min (salmon, bell pepper, broccoli, rice, soy sauce)
Saturday: Pan-seared salmon with lemon herb potatoes, 40 min (salmon, potato, lemon, green beans)
- Fish on Monday, Wednesday and Saturday: at most once a week.
[892 in, 336 out, finish: stop]
Monday: Honey-glazed salmon with roasted vegetables, 35 min (salmon, carrot, zucchini, honey)
Wednesday: Chicken and vegetable stir-fry with rice, 25 min (chicken, bell pepper, broccoli, rice, soy sauce)
Saturday: Lemon herb roast chicken thighs with potatoes, 50 min (chicken thigh, potato, lemon, green beans)
The plan keeps every rule.
```

The plans differ from run to run; a first plan that already keeps every rule ends after one round.

### With Aspire (optional)

This sample has an AppHost that starts the app and passes it the key from user secrets, stored as described in [docs/prerequisites.md](../../docs/prerequisites.md). From this folder:

```sh
aspire run
```

Open the dashboard link that `aspire run` prints and go to **Console logs**, resource `kitchen-plan`. The app runs once and exits; stop the AppHost with `Ctrl+C`. To read the output in the terminal instead, start the AppHost in the background and wait for the app to exit before reading its logs, or you may get only part of them:

```sh
aspire start
aspire wait kitchen-plan --status down
aspire logs kitchen-plan
aspire stop
```

## Run the tests

```sh
dotnet test
```

Thirty tests, no API calls:

- `HouseholdRulesTests` feed the check known plans: one that keeps every rule, peanuts, a weekday dinner of 40 minutes, a long dinner at the weekend, fish on two days, fish on three days named in one sentence, shrimp counted as fish and a dinner of 0 minutes.
- `StructuredOutputTests` run `RunAsync<DinnerPlan>` on a stubbed chat client: the reply is read into the record; the request carries a JSON schema named `DinnerPlan`, the same one `Program.cs` prints, with camelCase properties, the descriptions, `[Range]` as `minimum` and `maximum`, and `dinners` required at the top level, which a class with a settable `Dinners` list leaves out; minutes outside the range are read without complaint; a reply cut off at the token limit throws a `JsonException` from `Result`, and an empty one or `null` an `InvalidOperationException`; day names are read in any capitalization.
- `SchemaInPromptTests` run `SchemaInPrompt.RunAsync<DinnerPlan>` on a stubbed chat client: the schema goes into the message, the response format asks for JSON without a schema, a JSON answer is read into the record, `{}` reads as a plan whose `Dinners` is `null`, and an answer in prose throws a `JsonException` from `Result`.
- `RangeAttributeTests` show that `Validator.TryValidateObject` rejects a dinner outside the range but doesn't look inside a plan's list of dinners.
- `AppHostTests` check that the AppHost passes the secret `anthropic-api-key` parameter to the app as `ANTHROPIC_API_KEY`.

## Files

| File | What it is |
| --- | --- |
| `KitchenPlan/Program.cs` | Prints the schema, asks for a `DinnerPlan`, checks it and asks for fixes |
| `KitchenPlan/DinnerPlan.cs` | The `DinnerPlan` and `Dinner` records, with descriptions for the model |
| `KitchenPlan/HouseholdRules.cs` | The plain C# check of the household's rules |
| `KitchenPlan/SchemaInPrompt.cs` | Puts the schema in the prompt, for a provider without native structured output; `Program.cs` doesn't use it |
| `KitchenPlan.AppHost/AppHost.cs` | Starts the app with the API key |
| `KitchenPlan.Tests/` | Tests with a stubbed chat client |

The app holds the article's code as published. The article also shows two variations that aren't in the project, as output only: the output token limit lowered from 4096 to 100, and the same with the finish-reason check removed. The JSON fragment showing how Anthropic's client rewrites the `minutes` property, and the input-token count of the same request without a schema, came from a separate probe that logged the request; they aren't part of this sample.

Three more runs in the article use variations that aren't in the project either:

- the same program with the Ollama chat client from part 1 (`OllamaSharp` 5.5.0, `llama3.2`) in place of Anthropic's;
- `DinnerPlan` written as a class with a `Dinners { get; init; } = []` property, which leaves `required` out of the schema so Anthropic's client drops the format;
- both `planner.RunAsync<DinnerPlan>(...)` calls in `Program.cs` replaced by `SchemaInPrompt.RunAsync<DinnerPlan>(planner, ...)`.

## Try it

- Change the output token limit from 4096 to 100 and run again. The plan stops at the limit and the app reports `finish: length` instead of reading it. Remove the `FinishReason` check too, and `response.Result` throws a `JsonException` on the cut-off JSON.
- Remove the `[Description]` from `Minutes` and run again. The schema keeps the type and the range, and loses what the number means.
- Ask for "Dinners for Monday to Friday. We love satay." and see whether the check catches the peanuts, or whether they hide in an ingredient called "satay sauce".
