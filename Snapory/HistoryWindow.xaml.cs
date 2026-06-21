using System.Windows;
using Snapory.Models;
using Snapory.Services;

// Disambiguate from System.Windows.Localization (pulled in via System.Windows).
using Localization = Snapory.Services.Localization;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;

namespace Snapory;

/// <summary>
/// The history gallery: thumbnails of past screenshots, newest first, with a
/// menu mirroring the tray settings. Double-click a shot to reopen it in the
/// editor; delete one with its ✕ button, or clear them all.
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
        Gallery.ItemsSource = history.Items;

        RefreshMenuChecks();
        Activated += (_, _) => RefreshMenuChecks();
    }

    private void OnNew(object sender, RoutedEventArgs e) => NewRequested?.Invoke();

    private void OnClearAll(object sender, RoutedEventArgs e) => _history.Clear();

    private void OnDelete(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: Shot shot })
            _history.Remove(shot);
        e.Handled = true;
    }

    private void OnTileClick(object sender, MouseButtonEventArgs e)
    {
        // Double-click opens the shot back up in the editor.
        if (e.ClickCount == 2 && sender is FrameworkElement { DataContext: Shot shot })
            OpenRequested?.Invoke(shot);
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
}
