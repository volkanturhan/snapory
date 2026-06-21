using System.Windows;
using System.Windows.Controls;
using Snapory.Models;
using Snapory.Services;

// Disambiguate from System.Windows.Localization (pulled in via System.Windows).
using Localization = Snapory.Services.Localization;
using Clipboard = System.Windows.Clipboard;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;

namespace Snapory;

/// <summary>
/// The history gallery: a list of past screenshots down the right, and a large
/// preview of the selected one on the left. Selecting a shot just previews it
/// (no new window); the preview's buttons edit, copy, or delete it. A menu bar
/// mirrors the tray settings.
/// </summary>
public partial class HistoryWindow : Window
{
    private readonly CaptureHistory _history;

    /// <summary>Raised when the user asks to start a new capture.</summary>
    public event Action? NewRequested;

    /// <summary>Raised when the user opens a shot to edit it again.</summary>
    public event Action<Shot>? OpenRequested;

    /// <summary>Raised when the user picks About from the menu.</summary>
    public event Action? AboutRequested;

    public HistoryWindow(CaptureHistory history)
    {
        InitializeComponent();

        _history = history;
        ShotList.ItemsSource = history.Items;
        if (history.Items.Count > 0)
            ShotList.SelectedIndex = 0;

        UpdatePreview();
        RefreshMenuChecks();
        Activated += (_, _) => RefreshMenuChecks();
    }

    private Shot? Selected => ShotList.SelectedItem as Shot;

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) => UpdatePreview();

    // Show the selected shot in the preview pane (no new window), and enable the
    // actions only when something is selected.
    private void UpdatePreview()
    {
        var shot = Selected;
        PreviewImage.Source = shot?.LoadFull();
        EmptyState.Visibility = shot is null ? Visibility.Visible : Visibility.Collapsed;

        EditButton.IsEnabled = shot is not null;
        CopyButton.IsEnabled = shot is not null;
        DeleteButton.IsEnabled = shot is not null;
    }

    private void OnNew(object sender, RoutedEventArgs e) => NewRequested?.Invoke();

    private void OnClearAll(object sender, RoutedEventArgs e) => _history.Clear();

    private void OnEdit(object sender, RoutedEventArgs e)
    {
        if (Selected is { } shot)
            OpenRequested?.Invoke(shot);
    }

    private void OnListDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Selected is { } shot)
            OpenRequested?.Invoke(shot);
    }

    private void OnCopy(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } shot)
            return;

        try
        {
            Clipboard.SetImage(shot.LoadFull());
        }
        catch
        {
            // Ignore a transient clipboard failure rather than crash.
        }
    }

    private void OnDelete(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } shot)
            return;

        var index = ShotList.SelectedIndex;
        _history.Remove(shot);

        // Keep a sensible neighbour selected after the removal.
        if (ShotList.Items.Count > 0)
            ShotList.SelectedIndex = Math.Min(index, ShotList.Items.Count - 1);
        else
            UpdatePreview();
    }

    private void OnEnglish(object sender, RoutedEventArgs e)
    {
        Localization.Instance.Language = AppLanguage.English;
        RefreshMenuChecks();
    }

    private void OnTurkish(object sender, RoutedEventArgs e)
    {
        Localization.Instance.Language = AppLanguage.Turkish;
        RefreshMenuChecks();
    }

    private void OnToggleAutoStart(object sender, RoutedEventArgs e)
        => AutoStart.SetEnabled(AutoStartMenuItem.IsChecked);

    private void OnAbout(object sender, RoutedEventArgs e) => AboutRequested?.Invoke();

    private void RefreshMenuChecks()
    {
        EnglishMenuItem.IsChecked = Localization.Instance.Language == AppLanguage.English;
        TurkishMenuItem.IsChecked = Localization.Instance.Language == AppLanguage.Turkish;
        AutoStartMenuItem.IsChecked = AutoStart.IsEnabled();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Closing (X) just hides the gallery to the tray; the app keeps running
        // and is shut down from the tray's Quit command.
        e.Cancel = true;
        Hide();

        base.OnClosing(e);
    }
}
