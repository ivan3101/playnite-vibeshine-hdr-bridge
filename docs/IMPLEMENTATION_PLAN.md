# Vibeshine-native HDR refactor implementation plan

**Status:** reviewed and implemented for version 0.2.0. JSON/XML structure and
source consistency were checked locally. Executing the .NET test suites remains
the Windows/CI release gate because the current development machine has no .NET
SDK installed.

## Outcome

Replace the file/SMB manifest transport with Vibeshine's existing Playnite
connector and authenticated API. The host extension will classify games and
maintain one managed Playnite category. Vibeshine will transport the stable
Playnite ID and category membership. The client extension will consume that
metadata during its normal library synchronization and will continue to make an
explicit HDR or SDR request for every launch.

The project remains one repository containing two Playnite extensions. Existing
extension IDs are retained so installed copies can be upgraded in place. No
GitHub publication or Git commit is part of this work.

## Runtime contract

```text
Host Playnite game
  -> Force SDR tag / Force HDR tag / HDR feature / default SDR
  -> host extension adds or removes the managed "HDR Bridge: HDR" category
  -> Vibeshine's bundled Playnite connector sends id + categories over its pipe
  -> Vibeshine exposes the current data at GET /api/playnite/games
  -> client library sync caches stable Playnite ID + categories
  -> launch resolves category presence to HDR, absence to SDR
  -> Windows HDR is forced to that state before Moonlight starts
  -> Moonlight receives exactly --hdr or --no-hdr
```

## Implementation phases

### 1. Simplify the shared core

- Keep the existing fail-safe decision precedence:
  `Force SDR > Force HDR > configured HDR feature > SDR`.
- Replace manifest-specific result types with a small `HdrDecision` model.
- Add a pure category-membership planner so add/remove/no-op behavior can be
  unit tested without Playnite.
- Remove manifest serialization, file-cache, matching, schema, example, and
  their tests.

### 2. Refactor the host extension into a category synchronizer

- Keep the current extension ID for upgrade compatibility.
- Rename user-facing product/package text to **HDR Category Sync**.
- Replace `OutputPath` with configurable `HdrCategoryName`, defaulting to
  `HDR Bridge: HDR`.
- Reuse the current tag and PCGamingWiki feature settings.
- Resolve or create the managed category, remember its ID, and rename the same
  managed category when the setting changes.
- Batch-update only games whose membership actually changes.
- Remove the category for SDR and excluded/uninstalled games so stale HDR state
  fails closed.
- Continue debounced automatic updates and provide a manual synchronization
  menu item with an add/remove/unchanged summary.

### 3. Add first-class Vibeshine support to the client

- Rename the server flavor from `Vibepollo` to `Vibeshine` while retaining enum
  numeric value `3` for persisted settings compatibility.
- Detect Vibeshine by probing `/api/playnite/status` after the normal
  `/api/config` probe, including when the config otherwise resembles Sunshine.
- Send configured API tokens as Bearer authorization during initial detection.
- Implement a `VibeshineHostClient` that joins `/api/apps` with
  `/api/playnite/games` primarily by `playnite-id`, using a unique-name fallback
  only for compatibility with older app payloads.
- Continue supporting username/password fallback and certificate pinning.
- Continue supporting `/api/playnite/force_sync`, UUID cover retrieval, source
  metadata, categories, and Playnite IDs.

### 4. Replace client manifest resolution with category resolution

- Replace the manifest path setting with `HdrCategoryName` using the same
  default as the host.
- Resolve HDR only when the synchronized app has a stable Playnite ID and a
  non-null category snapshot; otherwise fail closed to SDR.
- Treat matching category presence as HDR and its absence as explicit SDR.
- Keep app-cache fallback as the last-known-good synchronized metadata.
- Preserve the existing Windows HDR switch, launch-cancellation option,
  post-stream SDR restoration, and explicit `--hdr`/`--no-hdr` behavior.
- Do not emit bitrate, resolution, FPS, codec, VSync, frame-pacing, audio, or
  decoder flags unless the user explicitly configured an existing override.

### 5. Settings and migration

- Keep serialized settings property names where practical so old settings load.
- Ignore obsolete manifest fields left in an existing Playnite settings file.
- Remove manifest-path validation and UI.
- Update help text and API-token scope guidance for Vibeshine.
- Keep the same plugin IDs; bump package and assembly versions to `0.2.0`.

### 6. Tests and verification

- Unit-test decision precedence and managed-category membership planning.
- Unit-test category-based HDR resolution, including case handling and missing
  ID/category fail-safe cases.
- Unit-test Vibeshine app/Playnite joining by stable ID, unique-name fallback,
  ambiguity rejection, and Bearer authentication behavior.
- Update existing server-type, metadata, settings-version, and launch tests.
- Run all cross-platform tests locally and perform compile/static checks that
  are available on this machine.
- Leave the Windows WPF/package build to the documented PowerShell build and CI
  path if the current non-Windows environment cannot execute it.

### 7. Documentation and packaging

- Rewrite the README and engineering handoff for the Vibeshine-native flow.
- Replace the manifest JSON schema with a configuration schema and example.
- Document exact Vibeshine API-token scopes and the required host/client setup.
- Rename release artifacts to `HdrCategorySync_v0.2.0.pext` and
  `SunshineLibraryVibeshineHdrBridge_v0.2.0.pext`.
- Retain CI packaging and release instructions.

## Reviewed risks and decisions

| Risk | Decision |
|---|---|
| Vibeshine does not transmit Playnite features or populated tags | Compute the final decision on the host and transmit only managed category membership. |
| A renamed game could match the wrong entry | Join by `playnite-id`; unique-name matching is fallback-only and rejects duplicates. |
| Missing API metadata could inherit Moonlight's previous HDR state | Resolve missing ID/category data to SDR and emit `--no-hdr`. |
| An old HDR category could remain after metadata changes | Host reconciliation explicitly removes the managed category from every game classified SDR. |
| Updating the category triggers another Playnite database event | Debounce and perform idempotent updates; a second pass produces no writes. |
| Changing the configured category name could leave an orphan marker | Persist the managed category ID and rename that object instead of creating a second marker. |
| Existing client settings contain a manifest path | Preserve extension identity and tolerate unknown/obsolete serialized fields; the new category default requires no migration input. |
| Vibeshine API data is unavailable during client sync | Retain the existing per-host last-known-good cache; if no trustworthy snapshot exists, fail to SDR. |
| Local HDR-Auto behavior could be affected | The host component only edits metadata and never changes Windows HDR or game launch actions. |

## Acceptance criteria

- No runtime component reads or writes `moonlight-metadata.json`.
- The host extension deterministically maintains one category from tags and
  PCGamingWiki feature metadata.
- A Vibeshine host is detected and returns app records with stable Playnite IDs
  and category snapshots.
- Every managed stream launch chooses exactly HDR or SDR and controls client
  Windows HDR before Moonlight starts.
- Moonlight's saved quality settings remain inherited.
- Tests pass, documentation describes the new flow, build outputs are renamed,
  and the repository remains uncommitted and unpublished.
