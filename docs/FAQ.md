# Setae FAQ

## Does Setae record my microphone?

No. Setae reads temporary microphone samples only long enough to calculate a relative input level. The application does not retain or write those samples to disk. The capture library and operating system may use temporary buffers while monitoring is active.

## Does Setae understand or transcribe speech?

No. It does not use speech recognition, transcription, or language analysis. Any sufficiently loud sound captured by the selected microphone can trigger an alert.

## Does Setae send audio or telemetry to the Internet?

No. Runtime processing is local and the MVP makes no network requests. There are no accounts, analytics, advertisements, or automatic update checks. Building the project and running CI are different: they restore packages from NuGet and therefore need Internet access.

## Does it work with Discord, games, or calls?

The design is independent of Discord, games, and calls as long as Windows exposes the microphone as an active capture device. Setae monitors the input independently and does not change the microphone volume, mute state, or default device. Compatibility with every device, game, and call application is still being validated.

## Is the meter showing real decibels?

No. The meter uses a stable relative 0-100 scale derived from the digital microphone signal. It is intended for personal feedback, not acoustic measurement or certification.

## Can short sounds trigger an alert?

The detector requires the level to remain above the configured threshold for the configured minimum duration. It also applies a configurable cooldown and hysteresis to avoid repeated alerts.

## What happens if I disconnect the microphone?

Monitoring stops and the application shows an error so that you can reconnect the device or select another input.

## Does Setae need administrator permissions?

No. It uses the microphone permission and capture devices exposed by Windows. The permission must be enabled in Windows; Setae does not grant it automatically.

## Where are preferences stored?

Preferences are stored locally in `%AppData%\setae\settings.json`. The file is plain JSON and contains configuration only, including the selected microphone identifier and window preferences. It contains no audio, transcripts, or usage history.

## Why does Windows show a SmartScreen warning?

Early releases are not Authenticode-signed yet. Windows may therefore show “Unknown publisher” or a SmartScreen warning for a new executable. Verify the SHA-256 hash published with the release and inspect the source commit before running it.

## Does Setae run in the system tray?

Not yet. Closing the window exits the current MVP. Background tray mode is a possible future feature, not a current capability.
