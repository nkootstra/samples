# Part 6: Telling an agent what day it is

A kitchen assistant asked to plan dinners for "the rest of this week". Without a date it has to guess; with `TodayContextProvider`, a context provider that adds "Today is Thursday 22 October 2026." to the instructions on every run, it plans from today. The app runs the question both ways and then prints the session history to show the date isn't stored in it. The date comes from a `TimeProvider`, so tests can fix the clock. This is the code from [Telling an agent what day it is](https://kootstra.io/writing/ai-agent-current-date-csharp/).

## What you need

Install these first (once for all samples), as described in [docs/prerequisites.md](../../docs/prerequisites.md):

- **.NET 10 SDK**
- **An Anthropic API key**
- **Aspire CLI**, optional: only to run the sample through its AppHost (see [With Aspire](#with-aspire-optional))

## Run it

From this folder, set the key for this terminal and run the app. `read -rs` asks for the key without echoing it, so it stays out of your shell history:

```sh
read -rs ANTHROPIC_API_KEY && export ANTHROPIC_API_KEY
dotnet run --project KitchenDate
```

The first plan has no date and usually covers a whole week; the second starts today. A run on a Thursday:

```text
I don't know which day it is, what's in your fridge, or what your household likes, so here's a generic plan you can adjust:

- **Monday:** Sheet-pan chicken thighs with roasted potatoes and green beans
[...]
Tell me which days are left, your dietary needs, and what ingredients you have, and I'll tailor it.
**Thursday:** Sheet-pan sausages with peppers and potatoes.
[...]
**Sunday:** Roast chicken with carrots and gravy.
user: Plan dinners for the rest of this week, one line per day.
assistant: **Thursday:** Sheet-pan sausages with peppers and potatoes.
[...]
```

The date is the date in the time zone of the machine the app runs on.

### With Aspire (optional)

This sample has an AppHost that starts the app and passes it the key from user secrets, stored as described in [docs/prerequisites.md](../../docs/prerequisites.md). From this folder:

```sh
aspire run
```

Open the dashboard link that `aspire run` prints and go to **Console logs**, resource `kitchen-date`. The app runs once and exits; stop the AppHost with `Ctrl+C`. To read the output in the terminal instead, start the AppHost in the background and wait for the app to exit before reading its logs, or you may get only part of them:

```sh
aspire start
aspire wait kitchen-date --status down
aspire logs kitchen-date
aspire stop
```

## Run the tests

```sh
dotnet test
```

Nine tests, no API calls:

- `TodayContextProviderTests` register the provider as `Program.cs` does, on a stubbed chat client and a `FakeTimeProvider` set to Wednesday 14 October 2026: the date is added to the instructions on its own line, every run reads the clock again, the date is the clock's local date (23:30 UTC is already Thursday in Amsterdam), it stays in English on a machine with a Dutch culture, and it reaches the model but stays out of the session history.
- `AlternativesTests` cover the two quick fixes the article compares: `DateInInstructions` fixes the date in the instructions when the agent is created, and `DateInMessage` stores the date in the history.
- `AppHostTests` check that the AppHost passes the secret `anthropic-api-key` parameter to the app as `ANTHROPIC_API_KEY`.

## Files

| File | What it is |
| --- | --- |
| `KitchenDate/Program.cs` | Asks the question without a date, then with the provider, and prints the history |
| `KitchenDate/TodayContextProvider.cs` | The context provider that adds today's date |
| `KitchenDate/Alternatives.cs` | The two quick fixes from the article; `Program.cs` doesn't use them |
| `KitchenDate.AppHost/AppHost.cs` | Starts the app with the API key |
| `KitchenDate.Tests/` | Tests with a stubbed chat client and a fake clock |

## Try it

- Write a small `TimeProvider` subclass that overrides `GetUtcNow` to return Saturday 17 October 2026 at 12:00 UTC and `LocalTimeZone` to return `TimeZoneInfo.Utc` (or use `FakeTimeProvider` from `Microsoft.Extensions.TimeProvider.Testing`, as the tests do), pass it to `TodayContextProvider` in `Program.cs` instead of `TimeProvider.System`, and run again. The plan should now cover only the weekend.
- Add a second line to the provider's `Instructions`, for example "The household does not eat meat.", and run again. The rule reaches every run without changing the agent's instructions or the stored history.
