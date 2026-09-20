using System;
using System.Collections.Generic;

namespace HdrBridge.Core
{
    public sealed class HdrDecision
    {
        public bool Hdr { get; set; }
        public string Decision { get; set; }
        public string MatchedFeature { get; set; }
    }

    public sealed class GameSnapshot
    {
        public string PlayniteId { get; set; }
        public string Name { get; set; }
        public bool IsInstalled { get; set; }
        public IReadOnlyCollection<string> Tags { get; set; } = Array.Empty<string>();
        public IReadOnlyCollection<string> Features { get; set; } = Array.Empty<string>();
    }

    public sealed class HdrClassificationOptions
    {
        public string ForceHdrTag { get; set; } = "Force HDR";
        public string ForceSdrTag { get; set; } = "Force SDR";
        public List<string> HdrFeatureNames { get; set; } = new List<string> { "HDR" };
        public bool InstalledGamesOnly { get; set; } = true;
    }

    public sealed class CategoryMembershipChange
    {
        public bool Changed { get; set; }
        public bool Added { get; set; }
        public bool Removed { get; set; }
        public List<Guid> CategoryIds { get; set; } = new List<Guid>();
    }
}
