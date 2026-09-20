using Microsoft.VisualStudio.TestTools.UnitTesting;
using SunshineLibrary.Models;
using SunshineLibrary.Services;
using System.Collections.Generic;

namespace SunshineLibrary.Tests
{
    [TestClass]
    public class HdrCategoryServiceTests
    {
        private static readonly HdrAutomationOptions Options = new HdrAutomationOptions
        {
            Enabled = true,
            HdrCategoryName = "HDR Bridge: HDR",
        };

        [TestMethod]
        public void ManagedCategory_EnablesHdrCaseInsensitively()
        {
            var result = new HdrCategoryService().Resolve(Options, App("hdr bridge: hdr"));

            Assert.IsTrue(result.Found);
            Assert.IsTrue(result.Hdr);
            Assert.AreEqual("vibeshine-category-hdr", result.Decision);
        }

        [TestMethod]
        public void ValidSnapshotWithoutMarker_IsExplicitSdr()
        {
            var result = new HdrCategoryService().Resolve(Options, App("Action"));

            Assert.IsTrue(result.Found);
            Assert.IsFalse(result.Hdr);
            Assert.AreEqual("vibeshine-category-sdr", result.Decision);
        }

        [TestMethod]
        public void MissingStablePlayniteId_FailsClosed()
        {
            var app = App("HDR Bridge: HDR");
            app.PlayniteId = null;

            var result = new HdrCategoryService().Resolve(Options, app);

            Assert.IsFalse(result.Found);
            Assert.IsFalse(result.Hdr);
            Assert.AreEqual("missing-playnite-id-default-sdr", result.Decision);
        }

        [TestMethod]
        public void MissingCategorySnapshot_FailsClosed()
        {
            var app = App();
            app.Categories = null;

            var result = new HdrCategoryService().Resolve(Options, app);

            Assert.IsFalse(result.Found);
            Assert.IsFalse(result.Hdr);
            Assert.AreEqual("missing-categories-default-sdr", result.Decision);
        }

        [TestMethod]
        public void DisabledAutomation_ReturnsNoDecision()
        {
            Assert.IsNull(new HdrCategoryService().Resolve(
                new HdrAutomationOptions { Enabled = false }, App("HDR Bridge: HDR")));
        }

        private static RemoteApp App(params string[] categories) => new RemoteApp
        {
            PlayniteId = "11111111-1111-1111-1111-111111111111",
            Name = "Game",
            Categories = new List<string>(categories),
        };
    }
}
