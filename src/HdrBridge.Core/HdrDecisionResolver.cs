using System;
using System.Collections.Generic;
using System.Linq;

namespace HdrBridge.Core
{
    public static class HdrDecisionResolver
    {
        public static HdrDecision Classify(GameSnapshot game, HdrClassificationOptions options)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            options = options ?? new HdrClassificationOptions();

            var tags = new HashSet<string>(
                (game.Tags ?? Array.Empty<string>()).Where(t => !string.IsNullOrWhiteSpace(t)),
                StringComparer.OrdinalIgnoreCase);

            // Force SDR deliberately wins if both override tags are present. This makes
            // contradictory metadata fail safe instead of unexpectedly enabling HDR.
            if (!string.IsNullOrWhiteSpace(options.ForceSdrTag) && tags.Contains(options.ForceSdrTag))
            {
                return Entry(game, false, "force-sdr", null);
            }

            if (!string.IsNullOrWhiteSpace(options.ForceHdrTag) && tags.Contains(options.ForceHdrTag))
            {
                return Entry(game, true, "force-hdr", null);
            }

            var featureNames = new HashSet<string>(
                (options.HdrFeatureNames ?? new List<string>()).Where(f => !string.IsNullOrWhiteSpace(f)),
                StringComparer.OrdinalIgnoreCase);
            var matched = (game.Features ?? Array.Empty<string>())
                .FirstOrDefault(f => !string.IsNullOrWhiteSpace(f) && featureNames.Contains(f));

            return matched == null
                ? Entry(game, false, "default-sdr", null)
                : Entry(game, true, "hdr-feature", matched);
        }

        private static HdrDecision Entry(GameSnapshot game, bool hdr, string decision, string feature)
        {
            return new HdrDecision
            {
                Hdr = hdr,
                Decision = decision,
                MatchedFeature = feature,
            };
        }
    }
}
