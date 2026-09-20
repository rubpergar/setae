# Setae: Offline Microphone Loudness Alert for Windows

Setae is a free-forever, open-source Windows utility that warns you when your microphone level stays too loud while you are wearing headphones. It is designed for gaming, voice chat, calls, shared spaces, and anyone who wants a quiet private reminder instead of speech recognition.

Audio is processed locally. Setae does not record, transcribe, recognize, upload, or analyze the content of your speech. It includes no ads, accounts, telemetry, or runtime network requests.

> Setae is an active work in progress. The MVP is implemented and the project is being validated with more hardware and longer sessions.

[Download and releases](../../releases) | [FAQ](docs/FAQ.md) | [Report a bug](../../issues/new?template=bug_report.yml) | [Request a feature](../../issues/new?template=feature_request.yml)

## Why Setae exists

Headphones make it harder to judge how loudly you are speaking. During a focused, tense, or exciting game session, your voice can gradually get louder without you noticing. Setae provides an external visual and optional audible reference without listening to what you say.

## Features

- Real-time level meter for the selected microphone, using a relative 0-100 scale.
- Configurable threshold, minimum time above the threshold, and cooldown between alerts.
- Optional short alert sound and green, amber, and red visual states.
- Compact dark window that can stay on top while monitoring.
- Local settings stored in `%AppData%\setae\settings.json`.
- Explicit handling for missing microphones, blocked access, device disconnects, and capture errors.
- No administrator privileges required; Windows microphone access must still be allowed.

## Privacy facts

| Property | Setae |
| --- | --- |
| Audio recording | No |
| Speech recognition or transcription | No |
| Network requests at runtime | No |
| Telemetry or analytics | No |
| Account required | No |
| Price | Free forever |
| Persistent data | Local preferences only |
| Processing | Local microphone level calculation |

Microphone samples are not retained or written to disk by Setae. Temporary buffers exist while the capture library processes the current input. The stored settings contain no audio, transcripts, or usage history. They can contain the selected microphone identifier and window preferences. The displayed level is relative to the digital microphone signal; it is not a certified acoustic dB SPL measurement.

## Download

Stable and test builds will be published in [GitHub Releases](../../releases). The intended distribution is a self-contained, single-file `win-x64` executable, so installing the .NET runtime is not required.

The current project is still in active MVP validation. Until a release is published, build the portable executable locally using the commands below.

### For users

1. Open the latest version in [GitHub Releases](../../releases).
2. Download `Setae-vX.Y.Z-win-x64.zip`, not `Code > Download ZIP`.
3. Extract the ZIP to a folder you control.
4. Run `Setae.App.exe`.
5. Allow microphone access in Windows privacy settings if Windows blocks it.

Setae is portable and does not install .NET, modify the registry, or require administrator privileges. Closing the window exits the application; the current MVP does not provide a background system-tray mode.

Windows may show an “Unknown publisher” or SmartScreen warning because early releases are not Authenticode-signed yet. Compare the SHA-256 value published with the release before running the file and review the source commit if you need to verify the build.

## Requirements

- Windows 10 or Windows 11.
- x64 hardware.
- A microphone that Windows exposes as an active capture device.

## Build, test, and publish

```text
dotnet build Setae.sln -c Release
dotnet test tests/Setae.App.Tests/Setae.App.Tests.csproj -c Release
dotnet build src/Setae.App/Setae.App.csproj -c Release -r win-x64
dotnet publish src/Setae.App/Setae.App.csproj -c Release -p:PublishProfile=portable
```

The installed application does not require administrator privileges and should continue to work when its network access is blocked by the Windows firewall. Building the project and running CI do require network access to restore SDK packages.

## Architecture

There is one production project organized by responsibility:

```text
src/Setae.App/
  Audio/          WASAPI capture, sample conversion, and level calculation
  Monitoring/     Smoothing, threshold, alert state machine, and preferences
  Infrastructure/ Persistence, alert sound, and single-instance handling
  UI/             Compact window and settings
```

The audio and monitoring layers do not depend on WPF or the file system. The UI reads the state published by the monitor.

## Scope and current limitations

Setae intentionally focuses on one job: helping you notice when your voice gets too loud. It does not provide speech recognition, recording, audio editing, advanced noise filtering, game-specific overlays, global hotkeys, profiles, automatic updates, accounts, or online services.

The MVP is implemented. Manual validation with integrated, USB, Bluetooth, and headset microphones, Discord, games, reconnects, scaling, firewall blocking, and long sessions remains part of the ongoing development work. System-tray background mode, automatic startup, and second-instance activation are not implemented in the current MVP.

## Contributing

Bug reports, hardware compatibility reports, documentation improvements, and focused pull requests are welcome. Read [CONTRIBUTING.md](CONTRIBUTING.md) before opening a change.

## License

Setae is released under the [MIT License](LICENSE).

See [docs/FAQ.md](docs/FAQ.md) for user questions, [CHANGELOG.md](CHANGELOG.md) for project history, [SECURITY.md](SECURITY.md) for vulnerability reports, and [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for dependency licenses.
