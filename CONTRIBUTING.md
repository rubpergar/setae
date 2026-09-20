# Contributing to Setae

Setae is a small Windows utility with a deliberately narrow scope: detect sustained microphone loudness locally and give the user a private reminder.

## Before opening an issue

- Search existing issues first.
- Include the app version or commit if known.
- Include the Windows version and microphone type.
- Do not attach recordings or other private audio.
- Explain the expected and actual behavior.

## Development setup

Requirements:

- Windows 10 or Windows 11.
- .NET SDK `10.0.100` or a compatible SDK allowed by `global.json`.

Build and test from the repository root:

```text
dotnet build Setae.sln -c Release
```

Create the portable application:

```text
dotnet publish src/Setae.App/Setae.App.csproj -c Release -p:PublishProfile=portable
```

## Design boundaries

- Keep audio processing local and do not retain or write microphone samples after calculating the level.
- Do not add speech recognition, recording, accounts, analytics, telemetry, or network requirements.
- Keep the audio and monitoring layers independent from WPF and persistence.
- Prefer focused changes with automated tests for monitoring behavior.
- Do not describe the relative level as a certified acoustic dB SPL measurement.

## Pull requests

Explain the user-visible behavior, include tests for behavior changes, and document any remaining manual validation. Keep pull requests focused and avoid unrelated refactors.
