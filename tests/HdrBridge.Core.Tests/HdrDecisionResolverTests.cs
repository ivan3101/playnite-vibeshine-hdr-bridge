using HdrBridge.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace HdrBridge.Core.Tests
{
    [TestClass]
    public class HdrDecisionResolverTests
    {
        private static readonly HdrClassificationOptions Options = new HdrClassificationOptions
        {
            ForceHdrTag = "Force HDR",
            ForceSdrTag = "Force SDR",
            HdrFeatureNames = new List<string> { "HDR", "HDR10" },
        };

        [TestMethod]
        public void ForceSdr_WinsOverEveryOtherSignal()
        {
            var game = Game(tags: new[] { "Force HDR", "Force SDR" }, features: new[] { "HDR" });
            var result = HdrDecisionResolver.Classify(game, Options);

            Assert.IsFalse(result.Hdr);
            Assert.AreEqual("force-sdr", result.Decision);
        }

        [TestMethod]
        public void ForceHdr_WinsWhenFeatureIsMissing()
        {
            var result = HdrDecisionResolver.Classify(Game(tags: new[] { "Force HDR" }), Options);
            Assert.IsTrue(result.Hdr);
            Assert.AreEqual("force-hdr", result.Decision);
        }

        [TestMethod]
        public void PcgamingwikiHdrFeature_EnablesHdrCaseInsensitively()
        {
            var result = HdrDecisionResolver.Classify(Game(features: new[] { "hdr" }), Options);
            Assert.IsTrue(result.Hdr);
            Assert.AreEqual("hdr-feature", result.Decision);
            Assert.AreEqual("hdr", result.MatchedFeature);
        }

        [TestMethod]
        public void MissingFeature_DefaultsToSdr()
        {
            var result = HdrDecisionResolver.Classify(Game(), Options);
            Assert.IsFalse(result.Hdr);
            Assert.AreEqual("default-sdr", result.Decision);
        }

        [TestMethod]
        public void EmptyFeatureConfiguration_DefaultsToSdr()
        {
            var options = new HdrClassificationOptions { HdrFeatureNames = new List<string>() };
            var result = HdrDecisionResolver.Classify(Game(features: new[] { "HDR" }), options);
            Assert.IsFalse(result.Hdr);
        }

        private static GameSnapshot Game(
            string id = "11111111-1111-1111-1111-111111111111",
            string name = "Game",
            bool installed = true,
            string[] tags = null,
            string[] features = null) => new GameSnapshot
            {
                PlayniteId = id,
                Name = name,
                IsInstalled = installed,
                Tags = tags ?? Array.Empty<string>(),
                Features = features ?? Array.Empty<string>(),
            };
    }
}
