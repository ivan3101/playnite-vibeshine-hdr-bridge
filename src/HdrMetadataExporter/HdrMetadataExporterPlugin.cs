using HdrBridge.Core;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Threading;

namespace HdrMetadataExporter
{
    public sealed class HdrMetadataExporterPlugin : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly ExporterSettingsViewModel settingsVm;
        private readonly DispatcherTimer debounceTimer;
        private bool subscribed;
        private bool synchronizing;

        public override Guid Id { get; } = Guid.Parse("8ec69983-0a95-4e65-9af4-459eb24c7547");

        public HdrMetadataExporterPlugin(IPlayniteAPI api) : base(api)
        {
            Properties = new GenericPluginProperties { HasSettings = true };
            settingsVm = new ExporterSettingsViewModel(this);
            debounceTimer = new DispatcherTimer(DispatcherPriority.Background, api.MainView.UIDispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(750),
            };
            debounceTimer.Tick += (_, __) =>
            {
                debounceTimer.Stop();
                SynchronizeNow(showSuccessNotification: false);
            };
        }

        public override ISettings GetSettings(bool firstRunSettings) => settingsVm;
        public override UserControl GetSettingsView(bool firstRunView) => new ExporterSettingsView { DataContext = settingsVm };

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            Subscribe();
            SynchronizeNow(showSuccessNotification: false);
        }

        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args) => ScheduleSynchronization();
        public override void OnGameInstalled(OnGameInstalledEventArgs args) => ScheduleSynchronization();
        public override void OnGameUninstalled(OnGameUninstalledEventArgs args) => ScheduleSynchronization();

        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            yield return new MainMenuItem
            {
                MenuSection = "@HDR Category Sync",
                Description = "Synchronize HDR category now",
                Action = _ => SynchronizeNow(showSuccessNotification: true),
            };
        }

        public void SynchronizeNow(bool showSuccessNotification)
        {
            if (synchronizing) return;
            var settings = settingsVm.Settings ?? new ExporterSettings();
            try
            {
                synchronizing = true;
                var categoryName = settings.HdrCategoryName?.Trim();
                if (string.IsNullOrWhiteSpace(categoryName))
                    throw new InvalidOperationException("HDR category name cannot be empty.");

                Guid previousCategoryId;
                Guid.TryParse(settings.ManagedCategoryId, out previousCategoryId);
                var category = ResolveManagedCategory(settings, categoryName);
                var games = PlayniteApi.Database.Games.ToList();
                var changed = new List<Game>();
                var hdrCount = 0;
                var added = 0;
                var removed = 0;

                foreach (var game in games)
                {
                    var decision = HdrDecisionResolver.Classify(ToSnapshot(game), settings.ToClassificationOptions());
                    var shouldHaveCategory = decision.Hdr && (!settings.InstalledGamesOnly || game.IsInstalled);
                    if (shouldHaveCategory) hdrCount++;

                    var currentIds = (game.CategoryIds ?? new List<Guid>()).ToList();
                    var removedLegacy = previousCategoryId != Guid.Empty && previousCategoryId != category.Id
                        && currentIds.Remove(previousCategoryId);
                    var membership = HdrCategoryMembership.Plan(currentIds, category.Id, shouldHaveCategory);
                    if (!membership.Changed && !removedLegacy) continue;

                    game.CategoryIds = membership.CategoryIds;
                    changed.Add(game);
                    if (membership.Added) added++;
                    if (membership.Removed || removedLegacy) removed++;
                }

                if (changed.Count > 0)
                {
                    using (PlayniteApi.Database.BufferedUpdate())
                    {
                        PlayniteApi.Database.Games.Update(changed);
                    }
                }

                logger.Info($"Synchronized HDR category '{category.Name}': {hdrCount} HDR, {added} added, {removed} removed, {changed.Count} changed.");

                if (showSuccessNotification)
                {
                    PlayniteApi.Notifications.Add(new NotificationMessage(
                        "hdr-category-sync-success",
                        $"HDR category synchronized: {hdrCount} HDR game(s), {added} added, {removed} removed.",
                        NotificationType.Info));
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "HDR category synchronization failed.");
                PlayniteApi.Notifications.Add(new NotificationMessage(
                    "hdr-category-sync-error",
                    "HDR category synchronization failed: " + ex.Message,
                    NotificationType.Error));
            }
            finally
            {
                synchronizing = false;
            }
        }

        public override void Dispose()
        {
            Unsubscribe();
            debounceTimer.Stop();
            base.Dispose();
        }

        private GameSnapshot ToSnapshot(Game game)
        {
            var tagNames = (game.TagIds ?? new List<Guid>())
                .Select(id => PlayniteApi.Database.Tags.Get(id)?.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToArray();
            var featureNames = (game.FeatureIds ?? new List<Guid>())
                .Select(id => PlayniteApi.Database.Features.Get(id)?.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToArray();

            return new GameSnapshot
            {
                PlayniteId = game.Id.ToString("D"),
                Name = game.Name,
                IsInstalled = game.IsInstalled,
                Tags = tagNames,
                Features = featureNames,
            };
        }

        private void Subscribe()
        {
            if (subscribed) return;
            PlayniteApi.Database.Games.ItemUpdated += GamesUpdated;
            PlayniteApi.Database.Games.ItemCollectionChanged += GamesChanged;
            PlayniteApi.Database.Tags.ItemUpdated += TagsUpdated;
            PlayniteApi.Database.Tags.ItemCollectionChanged += TagsChanged;
            PlayniteApi.Database.Features.ItemUpdated += FeaturesUpdated;
            PlayniteApi.Database.Features.ItemCollectionChanged += FeaturesChanged;
            PlayniteApi.Database.Categories.ItemUpdated += CategoriesUpdated;
            PlayniteApi.Database.Categories.ItemCollectionChanged += CategoriesChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            PlayniteApi.Database.Games.ItemUpdated -= GamesUpdated;
            PlayniteApi.Database.Games.ItemCollectionChanged -= GamesChanged;
            PlayniteApi.Database.Tags.ItemUpdated -= TagsUpdated;
            PlayniteApi.Database.Tags.ItemCollectionChanged -= TagsChanged;
            PlayniteApi.Database.Features.ItemUpdated -= FeaturesUpdated;
            PlayniteApi.Database.Features.ItemCollectionChanged -= FeaturesChanged;
            PlayniteApi.Database.Categories.ItemUpdated -= CategoriesUpdated;
            PlayniteApi.Database.Categories.ItemCollectionChanged -= CategoriesChanged;
            subscribed = false;
        }

        private void GamesUpdated(object sender, ItemUpdatedEventArgs<Game> args) => ScheduleSynchronization();
        private void GamesChanged(object sender, ItemCollectionChangedEventArgs<Game> args) => ScheduleSynchronization();
        private void TagsUpdated(object sender, ItemUpdatedEventArgs<Tag> args) => ScheduleSynchronization();
        private void TagsChanged(object sender, ItemCollectionChangedEventArgs<Tag> args) => ScheduleSynchronization();
        private void FeaturesUpdated(object sender, ItemUpdatedEventArgs<GameFeature> args) => ScheduleSynchronization();
        private void FeaturesChanged(object sender, ItemCollectionChangedEventArgs<GameFeature> args) => ScheduleSynchronization();
        private void CategoriesUpdated(object sender, ItemUpdatedEventArgs<Category> args) => ScheduleSynchronization();
        private void CategoriesChanged(object sender, ItemCollectionChangedEventArgs<Category> args) => ScheduleSynchronization();

        private void ScheduleSynchronization()
        {
            if (synchronizing || settingsVm.Settings?.SynchronizeOnLibraryChange != true) return;
            PlayniteApi.MainView.UIDispatcher.BeginInvoke(new Action(() =>
            {
                debounceTimer.Stop();
                debounceTimer.Start();
            }));
        }

        private Category ResolveManagedCategory(ExporterSettings settings, string categoryName)
        {
            Category managed = null;
            Guid managedId;
            if (Guid.TryParse(settings.ManagedCategoryId, out managedId))
                managed = PlayniteApi.Database.Categories.Get(managedId);

            var sameName = PlayniteApi.Database.Categories.FirstOrDefault(categoryItem =>
                string.Equals(categoryItem?.Name?.Trim(), categoryName, StringComparison.OrdinalIgnoreCase));

            if (managed == null)
            {
                managed = sameName ?? PlayniteApi.Database.Categories.Add(categoryName);
            }
            else if (!string.Equals(managed.Name, categoryName, StringComparison.Ordinal))
            {
                if (sameName != null && sameName.Id != managed.Id)
                {
                    managed = sameName;
                }
                else
                {
                    managed.Name = categoryName;
                    PlayniteApi.Database.Categories.Update(managed);
                }
            }

            var serializedId = managed.Id.ToString("D");
            if (!string.Equals(settings.ManagedCategoryId, serializedId, StringComparison.OrdinalIgnoreCase))
            {
                settings.ManagedCategoryId = serializedId;
                SavePluginSettings(settings);
            }
            return managed;
        }
    }
}
