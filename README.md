# samples

Runnable code for the articles on [kootstra.io](https://kootstra.io). Each sample is the code from one article, as a small .NET solution you can build, test and run on its own. Most samples also have an [Aspire](https://aspire.dev) AppHost that starts the app and passes it the API key it needs.

## What's here

| Folder | What it holds |
| --- | --- |
| [`agent-framework/`](agent-framework/) | The series [Microsoft Agent Framework in C#](agent-framework/README.md), starting with a first agent. |
| [`docs/`](docs/) | Setup shared by every sample: [prerequisites](docs/prerequisites.md). |

Each series or topic gets its own top-level folder, and each sample a numbered folder inside it.

## Prerequisites

You need the .NET 10 SDK and an Anthropic API key. Samples that run under Aspire also need the Aspire CLI and the key in .NET user secrets. [docs/prerequisites.md](docs/prerequisites.md) walks through all of it.

## Run a sample

Each sample's README has the exact commands. There are two ways to run one, both from the sample's folder.

With `dotnet run` and the key in the environment. `read -rs` asks for the key without echoing it, so it stays out of your shell history:

```sh
cd agent-framework/01-first-agent
read -rs ANTHROPIC_API_KEY && export ANTHROPIC_API_KEY
dotnet run --project FirstAgent
```

With Aspire, for a sample that has an AppHost. The AppHost reads the key from user secrets:

```sh
aspire run
```

`aspire run` builds the solution, starts the AppHost and prints a link to the Aspire dashboard, where the app's output is on the **Console logs** page. Stop the AppHost with `Ctrl+C`. To print the output in the terminal instead, start the AppHost in the background, wait for the app to exit, read its logs and stop the AppHost:

```sh
aspire start
aspire wait first-agent --status down
aspire logs first-agent
aspire stop
```

Reading the logs before the app has exited can return only part of its output. The resource name (`first-agent` here) is in each sample's README. A sample whose app reads console input has no AppHost, because Aspire doesn't connect a terminal to the app.

Run the tests from the same folder. They never call the model, so they need no API key and cost nothing:

```sh
dotnet test
```

## How a sample is organised

Every sample stands alone: nothing is shared between sample folders, so you can copy one out of the repository and it still builds, tests and keeps its formatting rules.

```text
01-first-agent/
├── FirstAgent/               the code from the article, unchanged
├── FirstAgent.AppHost/       Aspire AppHost: starts the app with the API key (most samples)
├── FirstAgent.Tests/         xUnit v3 tests, no API calls
├── FirstAgent.slnx           the solution
├── Directory.Build.props     net10.0, nullable, recommended analyzers, warnings as errors
├── Directory.Packages.props  package versions (central package management)
├── global.json               .NET 10 SDK, Microsoft.Testing.Platform for dotnet test
├── aspire.config.json        tells the Aspire CLI which AppHost to run (with an AppHost)
├── .editorconfig             formatting and code style
└── README.md
```

The app project contains the article's code as published. Where an article builds its program up step by step, the project holds the final version, and the README says which earlier steps are left out. The AppHost reads the secret parameter `anthropic-api-key` and hands it to the app as `ANTHROPIC_API_KEY`, the variable the article's code reads. Every AppHost uses the same user-secrets ID, `kootstra-samples`, so the key is stored once for all samples.

The tests replace only what crosses a boundary, such as the model, usually with an [NSubstitute](https://nsubstitute.github.io/) stub of `IChatClient` that returns scripted answers and records what it was sent. Everything else in a test is the real Agent Framework code. Where an article's program is top-level statements, the tests rebuild the same agent and check the behaviour the article relies on, rather than restructuring the article's code to make it testable.

## Continuous integration

[`.github/workflows/build.yml`](.github/workflows/build.yml) runs on every push to `main` and every pull request. It finds every folder with a `.slnx` file and, for each one in parallel, installs the SDK from that sample's `global.json`, restores with a NuGet cache, and runs `dotnet format --verify-no-changes` and `dotnet test`. It needs no secrets.

## License

[MIT](LICENSE), copyright (c) 2026 Niels Kootstra.
