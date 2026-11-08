# Part 13: Agent Framework DevUI in C# with Claude

A small ASP.NET Core app, the kitchen bench, that registers the kitchen assistant with its two pantry tools, the planner and the reviewer, and the `dinner-review` workflow that chains them. In Development it maps DevUI at `/devui/` together with the OpenAI Responses and Conversations endpoints DevUI calls; in any other environment none of them exist. Aspire starts the app with the API key and links to DevUI from the dashboard. This is the code from [Agent Framework DevUI in C# with Claude](https://kootstra.io/writing/agent-framework-devui-csharp/).

DevUI and the hosting packages are prereleases in 1.24.0: `Microsoft.Agents.AI.DevUI` and `Microsoft.Agents.AI.Hosting` are `1.24.0-preview.261006.1`, and `Microsoft.Agents.AI.Hosting.OpenAI` is `1.24.0-alpha.261006.1`. The app references only `Microsoft.Agents.AI.DevUI`; the other two come with it.

## What you need

Install these first (once for all samples), as described in [docs/prerequisites.md](../../docs/prerequisites.md):

- **.NET 10 SDK**
- **Aspire CLI**
- **An Anthropic API key**, stored in user secrets as `Parameters:anthropic-api-key` under the ID `kootstra-samples`

## Run it

From this folder:

```sh
aspire run
```

Open the dashboard link that `aspire run` prints. The `kitchen-bench` resource has one URL, **DevUI**, which opens `http://localhost:5121/devui/`. Pick `kitchen-assistant` and ask "What should I cook tonight so nothing goes to waste?", or pick `dinner-review`, choose **Configure & Run** and enter "Dinners for Monday, Wednesday and Saturday. We have salmon.". Every message calls Claude Haiku 5.5 on your key.

Unlike the console samples, this app keeps running until you stop it. To run it in the background instead:

```sh
aspire start
aspire wait kitchen-bench --status up
# open http://localhost:5121/devui/
aspire logs kitchen-bench
aspire stop
```

The log shows `Hosting environment: Development`. DevUI only answers requests from the same machine; from another machine the page and `/v1/entities` answer 403.

### Without Aspire

```sh
read -rs ANTHROPIC_API_KEY && export ANTHROPIC_API_KEY
dotnet run --project KitchenBench
```

The launch profile sets `ASPNETCORE_ENVIRONMENT=Development`, listens on `http://localhost:5121` and opens DevUI in the browser.

## Run the tests

```sh
dotnet test
```

22 tests, no API calls:

- `BenchTests` start the app in memory with `WebApplicationFactory` and a stubbed `IChatClient`. In Development, DevUI serves its page to a local caller, lists the kitchen assistant with its two tools and its instructions, and lists the workflow; a caller on another address, or a request with no remote address at all (which is what the in-memory test server sends), gets 403. The Responses endpoint still runs an agent for a caller on another address, and runs the workflow without `AddAsAIAgent`. In Production, `/devui/`, `/v1/entities`, `/v1/conversations` and `POST /v1/responses` all answer 404.
- `PantryToolsTests` cover the pantry tools, the same as in part 3.
- `AppHostTests` check that the AppHost passes the secret `anthropic-api-key` parameter to the app as `ANTHROPIC_API_KEY`.

The test server reports no remote address, which DevUI treats as a remote caller, so `BenchFactory` sets the address of each request with a small startup filter.

## Files

| File | What it is |
| --- | --- |
| `KitchenBench/Program.cs` | Registers the chat client, the agents and the workflow, and maps DevUI in Development |
| `KitchenBench/PantryTools.cs` | The pantry tools from part 3, unchanged; the article doesn't repeat them |
| `KitchenBench/Properties/launchSettings.json` | Port 5121, Development, opens `/devui` |
| `KitchenBench.AppHost/AppHost.cs` | Starts the app with the API key and adds the DevUI link |
| `KitchenBench.Tests/` | Tests with a stubbed chat client |

`Program.cs` and `AppHost.cs` match the article's code blocks line for line.

## Try it

- Start the app with `dotnet run --project KitchenBench -e ASPNETCORE_ENVIRONMENT=Production` and open `/devui/`. It answers 404, and so does a POST to `/v1/responses`. Setting the variable in the shell doesn't work: the launch profile sets Development.
- Lower the output limit in `AsIChatClient` to 64, ask the kitchen assistant a question in DevUI, and compare the output token count under the answer with the limit. DevUI still reports the response as complete.
