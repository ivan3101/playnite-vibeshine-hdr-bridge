# Playnite Vibeshine HDR Bridge v0.2.1

This release fixes client HDR detection after a Playnite or client restart.

- The client requests a fresh Vibeshine Playnite snapshot when a streaming app has incomplete category metadata. If that snapshot is still incomplete, it keeps the last good cached metadata.
- The client confirms category changes before replacing its cache. A confirmed removal of the managed HDR category launches in SDR, preserving `Force SDR` precedence.
- The host extension's classification logic is unchanged. SunshineLibrary continues to show imported host categories as client tags; HDR decisions use the host category snapshot.

Install `SunshineLibraryVibeshineHdrBridge_v0.2.1.pext` on the client and restart Playnite. Then run **Refresh Vibeshine host library** once. Existing `HdrCategorySync_v0.2.0` installations on the host can remain in place; the v0.2.1 host package contains no behavior change.
