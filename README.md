# Playnite Vibeshine HDR Bridge

Deterministic per-game HDR/SDR automation for a host Playnite → Vibeshine →
client Playnite → Moonlight setup.

This repository builds two Playnite extensions:

- **HDR Category Sync** runs on the gaming host. It classifies each game from
  Force HDR/Force SDR tags and PCGamingWiki HDR feature metadata, then maintains
  a dedicated Playnite category.
- **SunshineLibrary Vibeshine HDR Bridge** runs on the Moonlight client. It
  imports Vibeshine applications, consumes the synchronized Playnite ID and
  category data, switches client Windows HDR, and launches Moonlight with
  exactly `--hdr` or `--no-hdr`.

No SMB share, copied file, Syncthing folder, or extra metadata server is needed.
Vibeshine's bundled Playnite connector and authenticated HTTPS API are the
transport.

## Runtime flow

```text
HOST PC                                           CLIENT PC

Host Playnite game                               Imported streaming entry
  ├─ Force SDR tag ─┐                                  │
  ├─ Force HDR tag ─┤                                  ├─ stable Playnite ID
  └─ HDR feature ───┴─> HDR Bridge: HDR category       └─ category snapshot
                              │                              │
                              v                              v
                    Vibeshine Playnite pipe ─────> /api/playnite/games
                                                             │
                                          HDR category present?
                                             ├─ yes: Windows HDR ON + `--hdr`
                                             └─ no:  Windows HDR OFF + `--no-hdr`

Moonlight request ──> Vibeshine/Sunshine `dd_hdr_option = auto` ──> host HDR
```

HDR-Auto remains independent and can continue handling games launched locally
on the host. The host extension changes only Playnite metadata.

## Decision rules

The host always computes one final state:

1. `Force SDR` tag → SDR
2. `Force HDR` tag → HDR
3. configured HDR feature, default `HDR` → HDR
4. anything else → SDR

`Force SDR` deliberately wins if both override tags are present. HDR games get
the managed category; SDR and uninstalled/excluded games have it removed.

On the client, a stable Playnite ID and category snapshot are required. A
missing ID, missing snapshot, or invalid category configuration fails closed to
SDR and emits `--no-hdr`. A last-known-good app snapshot is retained for a host
that is temporarily offline.

## Moonlight settings

The HDR automation owns only HDR. It does not emit command-line overrides for:

- resolution, FPS, or bitrate;
- codec, VSync, or frame pacing;
- audio, decoder, renderer, or display mode.

Moonlight therefore continues loading the preferences saved in the same
Moonlight installation. Existing SunshineLibrary global, host, and per-game
overrides still work when explicitly configured.

## Requirements

- Windows 10 or Windows 11 on host and client;
- Playnite 10 with API 6.12 or newer;
- current Vibeshine with its bundled Playnite connector installed and active;
- Moonlight with `--hdr` and `--no-hdr` support;
- an HDR-capable client display and an HDR-capable physical or virtual host
  display;
- the PCGamingWiki Playnite metadata provider if automatic feature detection is
  desired.

## Build

On Windows with the .NET 8 SDK:

```powershell
git clone <your-repository-url>
cd playnite-vibeshine-hdr-bridge
./scripts/build.ps1
```

The script runs the test suites, builds both extensions, and creates:

```text
artifacts/HdrCategorySync_v0.2.0.pext
artifacts/SunshineLibraryVibeshineHdrBridge_v0.2.0.pext
```

## Host setup

1. Install `HdrCategorySync_v0.2.0.pext` in the host Playnite.
2. Confirm Vibeshine's bundled Playnite connector is installed and active.
   Configure Vibeshine to sync all installed games or your normal broad
   inclusion categories; do not use `HDR Bridge: HDR` as the only inclusion
   filter, because SDR games intentionally do not have that marker.
3. In the PCGamingWiki metadata provider, enable HDR video features and refresh
   feature metadata for streamed games.
4. Create the optional Playnite tags `Force HDR` and `Force SDR` for exceptions.
5. Open **Add-ons → Extension settings → HDR Category Sync**.
6. Keep the managed category at `HDR Bridge: HDR` unless you also change the
   client to the same value.
7. Run **HDR Category Sync → Synchronize HDR category now** once. Verify that an
   HDR game has the category and an SDR game does not.

The extension runs again after relevant game, tag, feature, category, install,
uninstall, and library changes. It updates only changed games and preserves all
unrelated categories.

## Vibeshine setup

Keep display HDR switching enabled and use:

```ini
dd_hdr_option = auto
```

Create a scoped API token for the client integration. It needs these routes:

```text
GET  /api/config
GET  /api/playnite/status
GET  /api/apps
GET  /api/playnite/games
GET  /api/apps/{uuid}/cover       optional, for cover downloads
POST /api/playnite/force_sync
POST /api/apps/close
```

Username/password authentication remains supported, but a scoped token is the
recommended client credential. The token is encrypted locally with Windows
DPAPI and is never written into normal Playnite plugin settings.

## Client setup

1. Install `SunshineLibraryVibeshineHdrBridge_v0.2.0.pext` in client Playnite.
   It retains SunshineLibrary's extension ID and replaces the upstream build;
   do not install both side by side.
2. Add the Vibeshine host. Use **Auto-detect** or choose **Vibeshine**, enter the
   API token, test the connection, and confirm the host certificate fingerprint.
3. Set the Moonlight executable or allow automatic detection.
4. Open the **HDR Automation** tab. Keep the category at
   `HDR Bridge: HDR` and leave the three safety controls enabled.
5. On **Streaming Defaults**, leave quality fields at **Inherit** if Moonlight's
   saved settings should remain authoritative.
6. Run **Refresh Vibeshine host library**. Repeat this after changing host HDR
   metadata if you want the client snapshot refreshed immediately.

## Verification matrix

| Starting client state | Host category | Expected client action | Expected request |
|---|---|---|---|
| SDR | present | Windows HDR on | `--hdr` |
| SDR | absent | Windows HDR remains off | `--no-hdr` |
| HDR after previous stream | absent | Windows HDR forced off | `--no-hdr` |
| HDR launch cancelled/stopped | present | client returns to SDR | stream ends |
| missing Playnite ID/categories | unavailable | Windows HDR forced off | `--no-hdr` |

Use Moonlight's statistics overlay to verify HDR/10-bit transport and confirm
that the saved bitrate, resolution, and FPS are unchanged.

## Configuration contract

The conceptual settings schema is in
[`docs/configuration.schema.json`](docs/configuration.schema.json), with a full
example in [`docs/example-configuration.json`](docs/example-configuration.json).
Playnite persists each extension's settings independently; the combined example
exists to document the values that must agree between machines.

## Development and release

```powershell
dotnet restore PlayniteHdrBridge.sln
dotnet build PlayniteHdrBridge.sln -c Release
dotnet test tests/HdrBridge.Core.Tests/HdrBridge.Core.Tests.csproj -c Release
dotnet test src/SunshineLibrary.HdrBridge/Tests/SunshineLibrary.Tests.csproj -c Release
./scripts/build.ps1
```

GitHub Actions runs the Windows build and test suites on every push and pull
request. Every successful push to `main` creates a uniquely tagged automated
prerelease containing both `.pext` packages and a copy of this README. Pull
requests build and test the project but never publish releases. Versioned stable
releases such as `v0.2.0` are created separately after validation.

For architecture, code ownership, migration behavior, security, testing, and
handoff details, see [`docs/HANDOFF.md`](docs/HANDOFF.md). The reviewed refactor
plan is preserved in [`docs/IMPLEMENTATION_PLAN.md`](docs/IMPLEMENTATION_PLAN.md).
