# Changelog

All notable changes to Setae will be documented here.

The project is in active MVP validation.

## [0.1.2] - 2026-09-20

### Changed

- Simplified end-user release notes with direct download and launch instructions.
- Updated GitHub Actions to current Node.js 24-compatible action versions.
- Made the release restore explicitly target `win-x64` before publishing.

### Fixed

- Release publishing no longer fails because the runtime-specific assets file is missing.

There are no runtime application behavior changes in this release.

## [0.1.1] - 2026-09-20

### Added

- First public portable Windows release.
- SHA-256 verification file and third-party license notices.

## Unreleased

### Added

- Public project documentation focused on local processing, privacy, and reproducible builds.
- MIT license and contribution guidance.
- Automated build and test workflow for Windows.
- Portable release workflow with a ZIP package and SHA-256 file.

### In progress

- Validation with integrated, USB, Bluetooth, and headset microphones.
- Long-session stability and resource measurements.
- Release packaging for `win-x64`.
- Authenticode signing and a system-tray background mode remain future work.
