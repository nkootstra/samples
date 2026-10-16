# Part 1: Microsoft Agent Framework: your first C# agent

A console app with one agent, a kitchen assistant on Claude Haiku 5.5. It asks one question and prints the answer, the token usage and the finish reason, warns when the answer stopped at the output token limit, then streams a second answer as it arrives. This is the code from [Microsoft Agent Framework: your first C# agent](https://kootstra.io/writing/maf-first-agent/).

## What you need

Install these first (once for all samples), as described in [docs/prerequisites.md](../../docs/prerequisites.md):

- **.NET 10 SDK**
- **An Anthropic API key**
- **Aspire CLI**, optional: only to run the sample through its AppHost (see [With Aspire](#with-aspire-optional))

## Run it

From this folder, set the key for this terminal and run the app. `read -rs` asks for the key without echoing it, so it stays out of your shell history:

```sh
read -rs ANTHROPIC_API_KEY && export ANTHROPIC_API_KEY
dotnet run --project FirstAgent
```

The answers differ on every run. The shape looks like this:

```text
Try a simple fried rice: sauté the diced onion in a little oil, scramble in the eggs, then add the cooked rice and stir-fry until heated through. Season with salt, pepper, or soy sauce if you have it.
[52 in, 75 out, finish: stop]
How about a classic crème brûlée? It's made with egg yolks, cream, and sugar, baked gently in a water bath, then topped with a crackly caramelized sugar layer just before serving.
[64 out]
```

### With Aspire (optional)

This sample has an AppHost that starts the app and passes it the key from user secrets, stored as described in [docs/prerequisites.md](../../docs/prerequisites.md). From this folder:

```sh
aspire run
```

Open the dashboard link that `aspire run` prints and go to **Console logs**, resource `first-agent`. The app runs once and exits; stop the AppHost with `Ctrl+C`. To read the output in the terminal instead, start the AppHost in the background and wait for the app to exit before reading its logs, or you may get only part of them:

```sh
aspire start
aspire wait first-agent --status down
aspire logs first-agent
aspire stop
```

## Run the tests

```sh
dotnet test
```

Five tests, no API calls. The article's code is top-level statements in `Program.cs`, so the tests build the same agent on a stubbed `IChatClient` and check what the article relies on: the instructions go out with every request, a truncated answer keeps its text and reports `ChatFinishReason.Length`, and streamed updates combine into one response with its usage. Two more check that the AppHost passes the secret `anthropic-api-key` parameter to the app as `ANTHROPIC_API_KEY`.

## Files

| File | What it is |
| --- | --- |
| `FirstAgent/Program.cs` | The whole app from the article: the agent, the usage and finish-reason check, and streaming |
| `FirstAgent.AppHost/AppHost.cs` | Starts the app with the API key |
| `FirstAgent.Tests/` | Tests with a stubbed chat client |

The article also shows how to swap Anthropic for an OpenAI-compatible endpoint or Ollama. Those versions need other packages and keys, so they are not in this sample; the article has the code.

## Try it

- In `Program.cs`, lower the output limit in `AsIChatClient("claude-haiku-5-5", 1024)` to 50 tokens and ask for a full recipe. The bracketed line reads `finish: length`, and `response.Text` may come back empty when the model spends all 50 tokens on reasoning.
- Swap the provider as the article shows (`dotnet add FirstAgent package Microsoft.Agents.AI.OpenAI --version 1.24.0` or `OllamaSharp`), run the same question and compare the answers. Only the statement that creates the chat client changes.
