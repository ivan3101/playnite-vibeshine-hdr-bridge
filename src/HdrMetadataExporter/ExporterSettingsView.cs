using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace HdrMetadataExporter
{
    public sealed class ExporterSettingsView : UserControl
    {
        public ExporterSettingsView()
        {
            Loaded += (_, __) =>
            {
                if (DataContext is ExporterSettingsViewModel vm) Content = Build(vm);
            };
        }

        private static UIElement Build(ExporterSettingsViewModel vm)
        {
            var root = new StackPanel { Margin = new Thickness(12) };
            root.SetResourceReference(TextElement.ForegroundProperty, "TextBrush");

            root.Children.Add(Heading("Vibeshine synchronization"));
            root.Children.Add(LabeledTextBox("Managed HDR category", vm.Settings.HdrCategoryName, value => vm.Settings.HdrCategoryName = value));
            root.Children.Add(Help("The extension maintains this Playnite category. Vibeshine's bundled Playnite connector sends the category and stable game ID to streaming clients; no shared manifest is required."));

            root.Children.Add(Heading("Decision rules"));
            root.Children.Add(LabeledTextBox("Force HDR tag", vm.Settings.ForceHdrTag, value => vm.Settings.ForceHdrTag = value));
            root.Children.Add(LabeledTextBox("Force SDR tag", vm.Settings.ForceSdrTag, value => vm.Settings.ForceSdrTag = value));

            root.Children.Add(new TextBlock { Text = "HDR feature names (one per line)", Margin = new Thickness(0, 10, 0, 3) });
            var features = new TextBox
            {
                AcceptsReturn = true,
                Height = 78,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Text = string.Join(Environment.NewLine, vm.Settings.HdrFeatureNames ?? Enumerable.Empty<string>()),
            };
            features.TextChanged += (_, __) => vm.Settings.HdrFeatureNames = features.Text
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Trim())
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            root.Children.Add(features);
            root.Children.Add(Help("Order: Force SDR → Force HDR → matching HDR feature → SDR. PCGamingWiki normally imports HDR as the feature named “HDR”."));

            root.Children.Add(CheckBox("Mark installed games only", vm.Settings.InstalledGamesOnly, value => vm.Settings.InstalledGamesOnly = value));
            root.Children.Add(CheckBox("Synchronize after Playnite library changes", vm.Settings.SynchronizeOnLibraryChange, value => vm.Settings.SynchronizeOnLibraryChange = value));

            return new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = root,
            };
        }

        private static TextBlock Heading(string text) => new TextBlock
        {
            Text = text,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 4, 0, 6),
        };

        private static TextBlock Help(string text) => new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 11,
            Opacity = 0.75,
            Margin = new Thickness(0, 3, 0, 12),
        };

        private static UIElement LabeledTextBox(string label, string initial, Action<string> changed)
        {
            var row = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };
            var caption = new TextBlock { Text = label, Width = 140, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(caption, Dock.Left);
            var box = new TextBox { Text = initial ?? string.Empty };
            box.TextChanged += (_, __) => changed(box.Text);
            row.Children.Add(caption);
            row.Children.Add(box);
            return row;
        }

        private static CheckBox CheckBox(string label, bool initial, Action<bool> changed)
        {
            var box = new CheckBox { Content = label, IsChecked = initial, Margin = new Thickness(0, 6, 0, 0) };
            box.Checked += (_, __) => changed(true);
            box.Unchecked += (_, __) => changed(false);
            return box;
        }
    }
}
