<div align="center">

<!-- IMAGE SLOT 1: add the logo at `docs/images/setae-logo.png` and place it here.
<img src="docs/images/setae-logo.png" alt="Setae logo" width="112">
-->

<h1>Setae</h1>

<p><strong>Private microphone loudness alerts for Windows</strong></p>

<p>
  <a href="https://github.com/rubpergar/setae/actions/workflows/ci.yml"><img src="https://github.com/rubpergar/setae/actions/workflows/ci.yml/badge.svg?branch=main" alt="Build and test status"></a>
  <a href="https://github.com/rubpergar/setae/releases"><img src="https://img.shields.io/github/v/release/rubpergar/setae?sort=semver" alt="Latest release"></a>
  <a href="https://github.com/rubpergar/setae/blob/main/LICENSE"><img src="https://img.shields.io/github/license/rubpergar/setae" alt="MIT license"></a>
  <img src="https://img.shields.io/badge/platform-Windows%2010%2F11-0078D4" alt="Windows 10 and 11">
</p>

<p>
  <a href="https://github.com/rubpergar/setae/releases">Download</a> |
  <a href="docs/FAQ.md">FAQ</a> |
  <a href="https://github.com/rubpergar/setae/issues">Issues</a> |
  <a href="CONTRIBUTING.md">Contribute</a>
</p>

</div>

Setae is a privacy-first Windows microphone monitor for gaming, voice chat, calls, streaming, and focused work. Headphones can make it difficult to notice when your voice gets louder; Setae gives you a quiet visual warning and, optionally, a short alert sound when the selected input remains above your chosen threshold.

It measures the level of the microphone signal, not speech content. There is no recording, transcription, account, advertising, telemetry, or runtime network connection.

## Why Setae?

- Notice sustained loudness before it becomes uncomfortable for other people.
- Keep a compact meter visible while gaming, calling, or wearing headphones.
- Get a private reminder without sending audio anywhere.
- Use it without an installer, .NET installation, or administrator privileges.

## See it in action

<!-- IMAGE SLOT 2: add the main interface screenshot at `docs/images/setae-main-window.png` and place it here.
<p align="center"><img src="docs/images/setae-main-window.png" alt="Setae main window showing the microphone level meter" width="520"></p>
-->

The main window shows a live relative level meter from 0 to 100, the configured threshold, the current monitoring state, and Start or Stop controls. The meter changes between normal, warning, and alert states as the input level changes.

## Features

- Real-time meter for the selected Windows input microphone.
- Configurable threshold from 0 to 100.
- Minimum loudness duration from 100 ms to 3 seconds to avoid reacting to brief peaks.
- Configurable cooldown from 0 to 30 seconds between alerts.
- Optional short alert sound and always-on-top window mode.
- Clear handling for missing microphones, denied permissions, disconnects, and capture errors.
- Portable, self-contained `win-x64` release with local-only processing.

## Configure it your way

<!-- IMAGE SLOT 3: add the settings screenshot at `docs/images/setae-settings.png` and place it here.
<p align="center"><img src="docs/images/setae-settings.png" alt="Setae settings panel" width="520"></p>
-->

Open **Settings** to choose the input microphone and tune the behavior to your voice and setup:

- **Threshold:** the relative level at which Setae considers the input too loud.
- **Minimum duration:** how long the level must stay above the threshold before an alert is triggered.
- **Cooldown:** the wait between alerts so repeated reminders do not become distracting.
- **Alert sound:** enable or disable the short audible reminder.
- **Always on top:** keep the meter visible over other windows.
- **Restore defaults:** return to the default monitoring values.

Preferences are stored locally in `%AppData%\setae\settings.json`. The file contains configuration only, never audio, transcripts, or usage history.

## Privacy by design

| Property | Setae |
| --- | --- |
| Records or saves microphone audio | No |
| Speech recognition or transcription | No |
| Runtime network requests | No |
| Accounts, ads, telemetry, or analytics | No |
| Stored data | Local preferences only |
| Processing | Local microphone-level calculation |

Microphone samples are held only in temporary buffers while the current level is calculated. Setae does not retain them or write them to disk. The meter is a relative 0-100 signal scale, not a certified acoustic dB SPL measurement.

## Download

Download the latest portable build from [GitHub Releases](https://github.com/rubpergar/setae/releases/latest):

1. Download `Setae-vX.Y.Z-win-x64.zip` from the release assets
2. Extract the ZIP to a folder you control
3. Run `Setae.App.exe`

Setae does not modify the registry and does not require administrator privileges. Windows microphone access must still be allowed in **Settings > Privacy & security > Microphone**.

Early releases are not Authenticode-signed yet, so Windows may show an Unknown publisher or SmartScreen warning. Verify the SHA-256 value published with the release before running the executable.

## Requirements

- Windows 10 or Windows 11
- x64 hardware
- An active microphone exposed by Windows

## Help and project links

- [FAQ](docs/FAQ.md) for privacy, compatibility, permissions, and troubleshooting questions.
- [Releases](https://github.com/rubpergar/setae/releases) for downloads and checksums.
- [Changelog](CHANGELOG.md) for project history.
- [Report a bug](https://github.com/rubpergar/setae/issues/new?template=bug_report.yml) or [request a feature](https://github.com/rubpergar/setae/issues/new?template=feature_request.yml).
- [Contributing guide](CONTRIBUTING.md) and [security policy](SECURITY.md).

## License

Setae is released under the [MIT License](LICENSE).
