# AppOogst

**AppOogst** (Dutch for *"app harvest"*) is a modern, dark-themed desktop app for macOS that lets you **harvest your own purchased App Store apps as `.ipa` files**. It is a friendly graphical front-end for the excellent open-source [ipatool](https://github.com/majd/ipatool) CLI.

Built with **C# / Avalonia 12** (MVVM), so the same codebase can later run on Windows and Linux as well.

## Features

- **Account** — sign in / sign out; credentials live in the macOS Keychain (managed by ipatool itself; AppOogst never sees or logs your password)
- **Search** — search the App Store by term, platform and result limit; buy a license with one click
- **Purchases** — browse the apps tied to your Apple ID with pagination; the list auto-loads on first visit and shows totals like "page 1 — 50 of 2704 apps"
- **Select to fill** — selecting a purchase automatically fills the Download form (App ID, Bundle ID, platform); double-click jumps straight to Download
- **Download** — fetch the `.ipa` into any folder, optionally auto-buying a missing license (`--purchase`)
- **Action queue** — actions clicked while another action is running are queued (max 3) and run automatically — no lost clicks
- **Color-coded log** — commands in blue, errors in red, successes in green
- **Headless verification** — built-in self-test and real-CLI probe modes

## Downloads (prebuilt binaries)

Grab a ready-to-run build from the [Releases](https://github.com/jphermans/AppOogst/releases) page — every `v*` tag triggers a GitHub Actions build:

| Package | Contents |
|---|---|
| `AppOogst-vX.Y.Z-win-x64.zip` | **`AppOogst.exe`** (single file) + bundled `ipatool.exe` — no .NET or ipatool install needed |
| `AppOogst-vX.Y.Z-osx-arm64.dmg` | real **AppOogst.app** (icon, ad-hoc signed) + bundled `ipatool` (Apple Silicon) |
| `AppOogst-vX.Y.Z-linux-x64.AppImage` | single-file **AppImage** + bundled `ipatool` |

All builds are **self-contained** (the .NET runtime is included) and ship with the official [ipatool](https://github.com/majd/ipatool) binary (MIT). Unzip/untar and run — no separate installation required. A system-installed ipatool is used as a fallback if present.

## Requirements (building from source)

- [.NET SDK 11](https://dotnet.microsoft.com/download)
- An Apple ID with App Store purchases (for actual downloads)

## Getting started

```bash
git clone https://github.com/jphermans/AppOogst.git
cd AppOogst
dotnet run
```

## Verification (no window required)

```bash
dotnet run -- --selftest   # 10 checks: argument building, JSON parsers, prefill, bundled-binary lookup
dotnet run -- --probe      # real CLI round-trip: auth info, search, list-purchases
```

## Usage

1. Open the **Account** page and sign in with your Apple ID (2FA supported). Credentials are stored by ipatool in the macOS Keychain.
2. **Search** for an app, or open **Purchases** to see everything you own.
3. Select an app — the **Download** form fills itself.
4. Hit **Start download** and find the `.ipa` in your chosen folder (default: `~/Downloads`).

## How it works

AppOogst runs `ipatool` with `--non-interactive --format json`, parses the JSON defensively and renders it in an Avalonia UI (Fluent dark theme). All CLI arguments are passed as an argument list — never through a shell — and passwords are never written to the log.

## Versioning

Releases are tagged with semantic version labels starting at `v0.1.0`. The version also lives in `AppOogst.csproj` as `<Version>`.

## License

[MIT](LICENSE) © 2026 Jean-Pierre Hermans

## Disclaimer

AppOogst is intended for backing up apps **you** have purchased. Use of ipatool must comply with Apple's terms of service; you are responsible for how you use this tool.
