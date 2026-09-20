using SunshineLibrary.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SunshineLibrary.Services.Hosts
{
    /// <summary>
    /// Vibeshine (Nonary/Vibeshine): Sunshine-compatible host that exposes Playnite
    /// library metadata via /api/playnite/games and accepts Bearer or Basic auth.
    ///
    /// ListAppsAsync fetches both /api/apps (for stable UUIDs and cover art) and
    /// /api/playnite/games (for PluginName and Categories), joining primarily on
    /// stable Playnite ID and using a unique-name fallback for older app payloads.
    /// A failed Playnite fetch fails the whole synchronization so SyncService keeps
    /// the last-good app/category cache instead of overwriting it with partial data.
    /// </summary>
    public sealed class VibeshineHostClient : ApolloHostClient
    {
        public override ServerType ServerType => ServerType.Vibeshine;

        public VibeshineHostClient(HostConfig config) : base(config)
        {
            ConfigureAuthorization(config);
        }

        internal VibeshineHostClient(HostConfig config, HttpMessageHandler handler) : base(config, handler)
        {
            ConfigureAuthorization(config);
        }

        /// <summary>
        /// Vibeshine accepts scoped Bearer tokens and HTTP Basic credentials directly.
        /// It does not use Apollo's legacy /api/login route, so no session bootstrap is
        /// required before a request.
        /// </summary>
        protected override Task<HostResult> EnsureSessionAsync(CancellationToken ct)
        {
            return Task.FromResult(ct.IsCancellationRequested ? HostResult.Cancelled() : HostResult.Ok());
        }

        // ── ListAppsAsync ─────────────────────────────────────────────────────────

        public override async Task<HostResult<IReadOnlyList<RemoteApp>>> ListAppsAsync(CancellationToken ct)
        {
            var login = await EnsureSessionAsync(ct).ConfigureAwait(false);
            if (!login.IsOk) return ToGeneric<IReadOnlyList<RemoteApp>>(login);

            // Primary fetch: /api/apps gives stable UUIDs used for cover art and GameId.
            var appsResult = await GetJsonAsync<ApolloAppsResponse>("api/apps", ct).ConfigureAwait(false);
            if (!appsResult.IsOk)
            {
                if (appsResult.Kind == HostResultKind.AuthFailed) InvalidateSession();
                return ToGeneric<IReadOnlyList<RemoteApp>>(appsResult.AsStatus());
            }

            // Enrichment fetch: /api/playnite/games gives stable identity + categories.
            // Do not return a partial snapshot: SyncService will retain the last-good cache.
            var playniteResult = await GetJsonAsync<List<VibeshinePlayniteGameDto>>("api/playnite/games", ct).ConfigureAwait(false);
            if (!playniteResult.IsOk)
            {
                if (playniteResult.Kind == HostResultKind.AuthFailed) InvalidateSession();
                return ToGeneric<IReadOnlyList<RemoteApp>>(playniteResult.AsStatus());
            }
            var lookups = BuildPlayniteLookups(playniteResult.Value);

            var apps = appsResult.Value?.Apps ?? new List<ApolloAppDto>();
            var list = new List<RemoteApp>(apps.Count);
            for (int i = 0; i < apps.Count; i++)
            {
                var a = apps[i];
                if (string.IsNullOrWhiteSpace(a?.Name)) continue;

                VibeshinePlayniteGameDto playnite = null;
                if (!string.IsNullOrWhiteSpace(a.PlayniteId))
                    lookups.ById.TryGetValue(a.PlayniteId, out playnite);
                if (playnite == null)
                    lookups.ByUniqueName.TryGetValue(a.Name, out playnite);

                list.Add(new RemoteApp
                {
                    StableId = !string.IsNullOrWhiteSpace(a.Uuid) ? a.Uuid : FallbackId(a.Name, a.Cmd, i),
                    PlayniteId = playnite?.Id ?? a.PlayniteId,
                    Name = a.Name,
                    Index = a.Index ?? i,
                    PluginName = playnite?.PluginName,
                    Categories = playnite?.Categories,
                    PlaytimeMinutes = playnite?.PlaytimeMinutes ?? 0,
                });
            }
            return HostResult<IReadOnlyList<RemoteApp>>.Ok(list);
        }

        // ── FetchCoverAsync ───────────────────────────────────────────────────────

        /// <summary>
        /// Vibeshine exposes covers at /api/apps/{uuid}/cover — UUID-based, no index needed.
        /// </summary>
        public override async Task<HostResult<byte[]>> FetchCoverAsync(RemoteApp app, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(app?.StableId) || app.StableId.StartsWith("fallback:", StringComparison.Ordinal))
                return HostResult<byte[]>.Ok(null);

            var login = await EnsureSessionAsync(ct).ConfigureAwait(false);
            if (!login.IsOk) return ToGeneric<byte[]>(login);

            var r = await GetBytesAsync($"api/apps/{app.StableId}/cover", ct).ConfigureAwait(false);
            if (r.Kind == HostResultKind.AuthFailed) InvalidateSession();
            return r;
        }

        // ── ForceSyncAsync ────────────────────────────────────────────────────────

        /// <summary>
        /// Tells Vibeshine to reconcile its Playnite library (POST /api/playnite/force_sync).
        /// Synchronous on the server side — returns when the host's app list is up to date.
        /// </summary>
        public override async Task<HostResult> ForceSyncAsync(CancellationToken ct)
        {
            var login = await EnsureSessionAsync(ct).ConfigureAwait(false);
            if (!login.IsOk) return login;

            var r = await PostJsonAsync("api/playnite/force_sync", null, null, ct).ConfigureAwait(false);
            if (r.Kind == HostResultKind.AuthFailed) InvalidateSession();
            return r;
        }

        // ── helpers ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Build a case-insensitive name → DTO map from the Playnite games list.
        /// Only installed games with a unique name are included. Ambiguous names are
        /// excluded so an app can never inherit another Playnite game's stable ID.
        /// Returns an empty map if the input is null.
        /// </summary>
        internal sealed class PlayniteLookups
        {
            public Dictionary<string, VibeshinePlayniteGameDto> ById { get; } =
                new Dictionary<string, VibeshinePlayniteGameDto>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, VibeshinePlayniteGameDto> ByUniqueName { get; } =
                new Dictionary<string, VibeshinePlayniteGameDto>(StringComparer.OrdinalIgnoreCase);
        }

        internal static PlayniteLookups BuildPlayniteLookups(
            List<VibeshinePlayniteGameDto> games)
        {
            var result = new PlayniteLookups();
            if (games == null) return result;
            var ambiguousIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ambiguousNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var g in games)
            {
                if (g == null || !g.Installed) continue;
                if (!string.IsNullOrWhiteSpace(g.Id) && !ambiguousIds.Contains(g.Id))
                {
                    if (result.ById.ContainsKey(g.Id))
                    {
                        result.ById.Remove(g.Id);
                        ambiguousIds.Add(g.Id);
                    }
                    else
                    {
                        result.ById[g.Id] = g;
                    }
                }
                if (string.IsNullOrWhiteSpace(g.Name)) continue;
                if (ambiguousNames.Contains(g.Name)) continue;
                if (result.ByUniqueName.ContainsKey(g.Name))
                {
                    result.ByUniqueName.Remove(g.Name);
                    ambiguousNames.Add(g.Name);
                    continue;
                }

                result.ByUniqueName[g.Name] = g;
            }
            return result;
        }

        private void ConfigureAuthorization(HostConfig config)
        {
            if (!string.IsNullOrEmpty(config.ApiToken))
            {
                Http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.ApiToken);
                return;
            }

            if (!string.IsNullOrEmpty(config.AdminUser))
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(
                    $"{config.AdminUser}:{config.AdminPassword ?? string.Empty}");
                Http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue(
                        "Basic", Convert.ToBase64String(bytes));
            }
        }

    }
}
