# Third-party notices

## SunshineLibrary

`src/SunshineLibrary.HdrBridge` is based on
[sharkusmanch/playnite-sunshine-library](https://github.com/sharkusmanch/playnite-sunshine-library),
commit `804606c68f6c0e8bed5e057aeee814d289cc4edc` (version 0.0.8), and retains its
original MIT license at `src/SunshineLibrary.HdrBridge/LICENSE`.

The HDR Bridge changes add Vibeshine Playnite-ID/category consumption,
deterministic HDR/SDR requests, client Windows HDR control, and a
Moonlight-settings-preserving runtime baseline.

## Playnite HDR control approach

`WindowsHdrController.cs` uses the documented Windows DisplayConfig API and
follows the primary-display selection approach used by Playnite's MIT-licensed
`HdrUtilities` implementation. It is implemented locally with only the native
structures required by this project.
