using HdrBridge.Core;
using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;

namespace HdrMetadataExporter
{
    public sealed class ExporterSettings
    {
        public string HdrCategoryName { get; set; } = "HDR Bridge: HDR";
        public string ManagedCategoryId { get; set; }
        public string ForceHdrTag { get; set; } = "Force HDR";
        public string ForceSdrTag { get; set; } = "Force SDR";
        public List<string> HdrFeatureNames { get; set; } = new List<string> { "HDR" };
        public bool InstalledGamesOnly { get; set; } = true;
        public bool SynchronizeOnLibraryChange { get; set; } = true;

        public HdrClassificationOptions ToClassificationOptions() => new HdrClassificationOptions
        {
            ForceHdrTag = ForceHdrTag,
            ForceSdrTag = ForceSdrTag,
            HdrFeatureNames = HdrFeatureNames ?? new List<string>(),
            InstalledGamesOnly = InstalledGamesOnly,
        };
    }

    public sealed class ExporterSettingsViewModel : ObservableObject, ISettings
    {
        private readonly HdrMetadataExporterPlugin plugin;
        private ExporterSettings editingClone;
        private ExporterSettings settings;

        public ExporterSettings Settings
        {
            get => settings;
            set
            {
                settings = value ?? new ExporterSettings();
                OnPropertyChanged();
            }
        }

        public ExporterSettingsViewModel(HdrMetadataExporterPlugin plugin)
        {
            this.plugin = plugin;
            Settings = plugin.LoadPluginSettings<ExporterSettings>() ?? new ExporterSettings();
        }

        public void BeginEdit() => editingClone = Serialization.GetClone(Settings);

        public void CancelEdit() => Settings = editingClone ?? new ExporterSettings();

        public void EndEdit()
        {
            plugin.SavePluginSettings(Settings);
            plugin.SynchronizeNow(showSuccessNotification: true);
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();
            if (string.IsNullOrWhiteSpace(Settings.HdrCategoryName)) errors.Add("HDR category name cannot be empty.");
            if (string.IsNullOrWhiteSpace(Settings.ForceHdrTag)) errors.Add("Force HDR tag cannot be empty.");
            if (string.IsNullOrWhiteSpace(Settings.ForceSdrTag)) errors.Add("Force SDR tag cannot be empty.");
            if (string.Equals(Settings.ForceHdrTag?.Trim(), Settings.ForceSdrTag?.Trim(), StringComparison.OrdinalIgnoreCase))
                errors.Add("Force HDR and Force SDR tags must have different names.");

            return errors.Count == 0;
        }
    }
}
