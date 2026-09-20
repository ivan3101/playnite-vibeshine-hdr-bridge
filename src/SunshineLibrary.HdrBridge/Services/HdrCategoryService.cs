using SunshineLibrary.Models;
using System;
using System.Linq;

namespace SunshineLibrary.Services
{
    public sealed class HdrCategoryResolution
    {
        public bool Hdr { get; set; }
        public bool Found { get; set; }
        public string Decision { get; set; }
        public string Warning { get; set; }
    }

    /// <summary>
    /// Resolves the final stream HDR mode from the category snapshot supplied by
    /// Vibeshine. Missing identity or metadata is deliberately treated as SDR.
    /// </summary>
    public sealed class HdrCategoryService
    {
        public HdrCategoryResolution Resolve(HdrAutomationOptions options, RemoteApp app)
        {
            if (options?.Enabled != true) return null;

            if (app == null || string.IsNullOrWhiteSpace(app.PlayniteId))
            {
                return FailSafe("missing-playnite-id-default-sdr",
                    "Vibeshine did not provide a stable Playnite ID for this app.");
            }

            if (app.Categories == null)
            {
                return FailSafe("missing-categories-default-sdr",
                    "Vibeshine did not provide a category snapshot for this app.");
            }

            var categoryName = options.HdrCategoryName?.Trim();
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                return FailSafe("invalid-category-default-sdr",
                    "The managed HDR category name is empty.");
            }

            var isHdr = app.Categories.Any(category =>
                string.Equals(category?.Trim(), categoryName, StringComparison.OrdinalIgnoreCase));
            return new HdrCategoryResolution
            {
                Hdr = isHdr,
                Found = true,
                Decision = isHdr ? "vibeshine-category-hdr" : "vibeshine-category-sdr",
            };
        }

        private static HdrCategoryResolution FailSafe(string decision, string warning) =>
            new HdrCategoryResolution
            {
                Hdr = false,
                Found = false,
                Decision = decision,
                Warning = warning,
            };
    }
}
