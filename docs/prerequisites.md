# Prerequisites

Every sample in this repository needs the .NET 10 SDK and an Anthropic API key. Samples that run under Aspire, and the optional Aspire runs of the others, also need the Aspire CLI. Install them once; each sample's README links back here.

## 1. Install the .NET 10 SDK

Download the SDK for your platform from [dotnet.microsoft.com/download/dotnet/10.0](https://dotnet.microsoft.com/download/dotnet/10.0) and install it. Check that a 10.0 SDK is listed:

```sh
dotnet --list-sdks
```

```text
10.0.302 [/usr/local/share/dotnet/sdk]
```

Each sample has a `global.json` that asks for SDK 10.0.100 or any later 10.0 release, so an older SDK is not picked up by accident.

## 2. Install the Aspire CLI

Some samples are started through [Aspire](https://aspire.dev), and most of the others can be: a small AppHost project starts the sample app, hands it the API key and shows its output in the Aspire dashboard. The CLI installs with one command.

macOS and Linux:

```sh
curl -sSL https://aspire.dev/install.sh | bash
```

Windows (PowerShell):

```powershell
irm https://aspire.dev/install.ps1 | iex
```

Open a new terminal and check the version. The samples were built with Aspire 13.6.1:

```sh
aspire --version
```

An older CLI updates itself with `aspire update --self`.

## 3. Get an Anthropic API key and store it

The samples call Anthropic's Claude Haiku 5.5, so each run costs a few tokens on your Anthropic account.

1. Sign in to the [Claude Console](https://platform.claude.com/settings/keys) and create an API key.
2. Store it in .NET user secrets, under the ID that every sample's AppHost shares (`kootstra-samples`). `read -rs` asks for the key without echoing it, so it doesn't end up in your shell history:

   ```sh
   read -rs KEY && dotnet user-secrets set --id kootstra-samples Parameters:anthropic-api-key "$KEY"; unset KEY
   ```

   On Windows (PowerShell):

   ```powershell
   $key = Read-Host -MaskInput "Anthropic API key"
   dotnet user-secrets set --id kootstra-samples Parameters:anthropic-api-key $key
   Remove-Variable key
   ```

   User secrets live in your user profile (`~/.microsoft/usersecrets/kootstra-samples/secrets.json` on macOS and Linux, `%APPDATA%\Microsoft\UserSecrets\kootstra-samples\secrets.json` on Windows), never in the repository. You set the key once and every sample's AppHost finds it. User secrets are a plain JSON file, not encrypted storage: they keep the key out of the repository, nothing more.

3. Check that it is there without printing it:

   ```sh
   dotnet user-secrets list --id kootstra-samples | cut -d= -f1
   ```

   ```text
   Parameters:anthropic-api-key
   ```

The AppHost reads `Parameters:anthropic-api-key` as a secret parameter and passes it to the sample app as the `ANTHROPIC_API_KEY` environment variable, which is what the code in the articles reads.

### Running a sample without Aspire

Every sample app also runs on its own with `dotnet run`. It then reads `ANTHROPIC_API_KEY` from the environment, so set that in the terminal first. `read -s` keeps the key out of your shell history:

```sh
read -rs ANTHROPIC_API_KEY && export ANTHROPIC_API_KEY
```

On Windows (PowerShell):

```powershell
$env:ANTHROPIC_API_KEY = Read-Host -MaskInput "Anthropic API key"
```

Never put the key in a file inside the repository: no `appsettings.json`, no `.env`, no `launchSettings.json`. The `.gitignore` excludes `.env` files and `secrets.json`, but the safe place is user secrets or your environment.
