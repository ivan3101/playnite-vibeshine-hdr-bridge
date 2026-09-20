namespace SunshineLibrary.Models
{
    /// <summary>
    /// Flavor-agnostic app representation consumed by library code. Built from
    /// SunshineAppDto or ApolloAppDto by the corresponding HostClient subclass.
    /// </summary>
    public class RemoteApp
    {
        /// <summary>Stable id within a host: Apollo/Vibeshine uuid, or sha256(name|cmd) for Sunshine.</summary>
        public string StableId { get; set; }

        /// <summary>
        /// Host Playnite database id exposed by Vibeshine. This is the authoritative
        /// identity used with the synchronized category snapshot.
        /// </summary>
        public string PlayniteId { get; set; }

        public string Name { get; set; }

        /// <summary>Sunshine/Apollo: app index in apps.json array. Used for admin endpoints like /appasset/{index}.</summary>
        public int? Index { get; set; }

        public string CoverRelativePath { get; set; }

        /// <summary>Vibeshine: library source display name from Playnite (e.g. "Steam", "GOG"). Null for Sunshine/Apollo.</summary>
        public string PluginName { get; set; }

        /// <summary>Vibeshine Playnite categories. Null means no trustworthy snapshot; an empty list means explicit SDR metadata.</summary>
        public System.Collections.Generic.List<string> Categories { get; set; }

        /// <summary>
        /// Vibeshine: host-side playtime in minutes from Playnite. Zero until the API exposes this field.
        /// </summary>
        public ulong PlaytimeMinutes { get; set; }
    }
}
