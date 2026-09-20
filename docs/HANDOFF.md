# Playnite Vibeshine HDR Bridge — engineering handoff

## 1. Scope

This repository fills the HDR decision and client-display-control gap in this
pipeline:

```text
host Playnite -> Vibeshine Playnite connector -> Vibeshine API
              -> client Playnite library plugin -> Moonlight
```

It owns host classification, managed category membership, Vibeshine metadata
consumption, client Windows HDR switching, and explicit Moonlight HDR/SDR
requests. It does not replace Playnite, Vibeshine, Moonlight, PCGamingWiki, or
HDR-Auto. HDR-Auto remains independent for local host launches.

## 2. Outputs and compatibility

| Package | Machine | Responsibility |
|---|---|---|
| `HdrCategorySync_v0.2.0.pext` | host | Classify games and maintain the managed HDR category |
| `SunshineLibraryVibeshineHdrBridge_v0.2.0.pext` | client | Import apps, resolve HDR, control Windows HDR, launch Moonlight |

The host package retains extension ID
`HdrMetadataExporter_8ec69983-0a95-4e65-9af4-459eb24c7547`; the source directory
and assembly keep their historical name so 0.1 upgrades in place.

The client retains SunshineLibrary extension ID
`SunshineLibrary_496637e1-1607-4016-aa4b-d6f732c21210`. Upstream
SunshineLibrary and this fork must not be installed simultaneously.

`ServerType.Vibeshine` keeps numeric value `3`, formerly labeled `Vibepollo`, so
an existing serialized host remains valid.

## 3. Technologies

- C#, .NET Framework 4.6.2, and an additional .NET 8 shared-core test target.
- WPF/code-built settings controls and Playnite SDK 6.16.0.
- Newtonsoft.Json 10.0.3 as a Playnite-compatible compile-only dependency.
- HTTPS with response limits, leaf-certificate SHA-256 pinning, scoped Bearer tokens,
  password fallback, and Windows DPAPI credential storage.
- Windows DisplayConfig P/Invoke for client HDR control.
- MSTest, PowerShell packaging, and GitHub Actions on `windows-latest`.

## 4. Structure

```text
PlayniteHdrBridge.sln
README.md
.github/workflows/ci.yml
scripts/build.ps1

docs/
  IMPLEMENTATION_PLAN.md
  HANDOFF.md
  configuration.schema.json
  example-configuration.json

src/
  HdrBridge.Core/
  HdrMetadataExporter/          # user-facing name: HDR Category Sync
  SunshineLibrary.HdrBridge/

tests/HdrBridge.Core.Tests/
```

## 5. Shared core

`HdrModels.cs` contains classification and membership results.
`HdrDecisionResolver.cs` implements
`Force SDR > Force HDR > HDR feature > SDR`.
`HdrCategoryMembership.cs` plans add/remove/no-op membership while preserving
unrelated categories.

The old JSON serializer, file cache, and name matcher were removed. There is no
runtime file transport.

## 6. Host extension

The host plugin resolves or creates the configured category, persists its ID,
and renames the same object when configuration changes. It classifies every game
and batch-updates only changed rows. HDR games receive the marker; SDR and
uninstalled games lose it. A category-ID collision is handled by adopting the
desired existing category and removing the old managed ID from games.

The plugin synchronizes on startup and after relevant database events with a
750 ms debounce. Its own writes are guarded and idempotent.

Conceptual settings:

```json
{
  "hdrCategoryName": "HDR Bridge: HDR",
  "forceHdrTag": "Force HDR",
  "forceSdrTag": "Force SDR",
  "hdrFeatureNames": ["HDR"],
  "installedGamesOnly": true,
  "synchronizeOnLibraryChange": true
}
```

`managedCategoryId` is internal state and should not be hand-edited.

## 7. Vibeshine transport

Vibeshine's bundled connector sends game `id`, `name`, `categories`, install
state, and plugin metadata through its local pipe. Vibeshine exposes that cached
snapshot through authenticated `GET /api/playnite/games`; auto-synced app
objects also carry `playnite-id` and a stable UUID.

The client joins `/api/apps` with `/api/playnite/games` by `playnite-id`. For an
older app payload without the field, it allows a name fallback only when that
Playnite name is unique. Duplicate names are rejected.

The marker category is required because current Vibeshine transport does not
expose Playnite features and its bundled connector currently emits an empty tag
array. The final HDR boolean is therefore computed on the host.

The marker must not be the user's sole Vibeshine inclusion category: SDR games
deliberately lack it. Vibeshine should continue syncing all installed games or
the user's normal broad inclusion categories.

## 8. Client host layer

`Services/Hosts/` contains the common pinned HTTPS client and implementations
for Sunshine, Apollo, and Vibeshine. Detection reads `/api/config`, then probes
`/api/playnite/status` for both Sunshine- and Apollo-shaped compatible servers.

`VibeshineHostClient` provides stable ID/category enrichment, UUID cover
retrieval, and `/api/playnite/force_sync`. Bearer tokens are stored only in a
per-host DPAPI blob and never serialized into `HostConfig`.

A failed Playnite-metadata request fails the synchronization as a whole. The
sync layer then yields the last-good cached app/category snapshot instead of
overwriting it with a partial `/api/apps` result.

## 9. Client HDR resolution

`HdrCategoryService` applies this fail-safe table:

```text
automation disabled                 -> no bridge decision
missing stable Playnite ID          -> SDR
missing category snapshot           -> SDR
invalid category configuration      -> SDR
managed category present            -> HDR
valid snapshot, category absent     -> SDR
```

An empty category list is explicit SDR; null means enrichment was unavailable.
`AppCache` retains the last successful authenticated host snapshot for offline
use. The dedicated refresh action force-syncs Vibeshine before pulling data.

## 10. Launch lifecycle

The root plugin resolves the cached remote app, resolves HDR, merges existing
stream overrides, and changes only the nullable HDR field. It switches Windows
HDR in `OnGameStarting`, can cancel on failure, restores SDR on cancel/stop, and
asks Vibeshine to close the host app afterward.

`MoonlightCompatibleClient` strips HDR tokens from advanced arguments and emits
exactly one authoritative `--hdr` or `--no-hdr`. All non-HDR fields default to
`Inherit`, preserving Moonlight's saved quality settings.

## 11. Windows HDR

The client uses DisplayConfig APIs and controls the active target whose source
is at desktop coordinate `(0, 0)`. Multi-monitor users streaming on a
non-primary display may require future display selection support. Failures are
surfaced and can cancel launch.

## 12. Migration

Client settings schema version is `3`.

- obsolete `HdrMetadata.SourcePath` values are ignored;
- missing `HdrCategoryName` initializes to `HDR Bridge: HDR`;
- server flavor numeric value `3` now displays as Vibeshine;
- plugin IDs remain unchanged.

## 13. Tests and build

Tests cover decision precedence, membership planning, category HDR resolution,
missing-metadata fail-safe behavior, stable-ID joins, duplicate-name rejection,
Bearer authorization, launch arguments, caches, credentials, sync metadata,
and settings migration.

Run on Windows:

```powershell
./scripts/build.ps1
```

The current Mac development environment does not include the .NET SDK, so this
refactor could not execute the test runner locally. CI and a Windows development
machine are the required compilation gate before installation.

## 14. Release checklist

1. Run `./scripts/build.ps1` on Windows and require both test projects to pass.
2. Inspect both `.pext` archives for expected extension files.
3. Test upgrading a copy of a 0.1 Playnite profile.
4. Test category add/remove, duplicate names, Vibeshine refresh, and the README
   verification matrix.
5. Commit only after review. This workspace is intentionally uncommitted and
   unpublished.

## 15. Known limitations

- Feature classification depends on metadata already imported into host
  Playnite; unknown games default to SDR.
- The client snapshot changes during library refresh. Use the dedicated refresh
  action after changing host metadata for immediate effect.
- An offline cache may be stale but is the last authenticated snapshot. With no
  snapshot, HDR fails closed.
- The project does not patch or fork Vibeshine; it uses existing public fields
  and endpoints.
