using Newtonsoft.Json.Linq;
using SunshineLibrary.Models;
using System.Threading;
using System.Threading.Tasks;

namespace SunshineLibrary.Services.Hosts
{
    /// <summary>
    /// Probes /api/config and constructs the right subclass. Caches the detected
    /// flavor on the HostConfig so subsequent runs skip the probe.
    /// </summary>
    public static class HostClientFactory
    {
        public static HostClient Create(HostConfig config)
        {
            switch (config.ServerType)
            {
                case ServerType.Vibeshine: return new VibeshineHostClient(config);
                case ServerType.Apollo: return new ApolloHostClient(config);
                case ServerType.Sunshine: return new SunshineHostClient(config);
                default: return new SunshineHostClient(config); // safe default until probed
            }
        }

        /// <summary>
        /// Probe <c>/api/config</c> and classify the server (PLAN §6).
        /// Both Sunshine and Apollo respond with <c>{ status, platform, version, ...config }</c>;
        /// Apollo additionally injects <c>vdisplayStatus</c> on Windows builds. We use that as
        /// the primary discriminator. Verified against:
        ///   - LizardByte/Sunshine confighttp.cpp (getConfig handler — no Apollo-only keys)
        ///   - ClassicOldSong/Apollo confighttp.cpp (getConfig handler — adds vdisplayStatus on _WIN32)
        ///
        /// If the response carries the standard Sunshine shape (<c>platform</c> + <c>version</c>)
        /// but no Apollo markers, we treat it as Sunshine — a reachable, parseable
        /// /api/config means the server is API-compatible. Returning Unknown in that
        /// case would block sync entirely, which is worse than mis-classifying a
        /// non-Windows Apollo install as Sunshine (only consequence: sha256 app ids
        /// instead of uuids, still functional).
        /// </summary>
        public static async Task<ServerType> ProbeServerTypeAsync(HostClient client, CancellationToken ct)
        {
            var result = await client.ProbeConfigAsync(ct).ConfigureAwait(false);
            var serverType = ClassifyConfig(result);

            // Apollo protects /api/config with session-cookie auth. If a non-Apollo client
            // (e.g. the SunshineHostClient default used for unknown types) got a 401, retry
            // with an ApolloHostClient so it can log in first.
            if (serverType == ServerType.Unknown && result.Kind == HostResultKind.AuthFailed && !(client is ApolloHostClient))
            {
                using (var apolloClient = new ApolloHostClient(client.Config))
                {
                    var apolloResult = await apolloClient.ProbeConfigAsync(ct).ConfigureAwait(false);
                    serverType = ClassifyConfig(apolloResult);
                }
            }

            // Vibeshine has evolved from the Sunshine/Apollo family and can resemble
            // either shape. Probe its Playnite endpoint after every successful compatible
            // config classification instead of relying on an Apollo-only marker.
            if (serverType == ServerType.Apollo || serverType == ServerType.Sunshine)
            {
                var vibeshineClient = client as VibeshineHostClient;
                bool owned = vibeshineClient == null;
                if (owned) vibeshineClient = new VibeshineHostClient(client.Config);
                try
                {
                    if (await vibeshineClient.IsVibeshineAsync(ct).ConfigureAwait(false))
                        return ServerType.Vibeshine;
                }
                finally
                {
                    if (owned) vibeshineClient.Dispose();
                }
            }

            return serverType;
        }

        internal static ServerType ClassifyConfig(HostResult<JObject> result)
        {
            if (!result.IsOk || result.Value == null) return ServerType.Unknown;
            var raw = result.Value;

            // Apollo-only marker (Windows builds): virtual display driver status.
            if (raw["vdisplayStatus"] != null) return ServerType.Apollo;

            // Standard Sunshine/Apollo shape — both ship platform + version at the top level.
            if (raw["platform"] != null && raw["version"] != null) return ServerType.Sunshine;

            return ServerType.Unknown;
        }
    }
}
