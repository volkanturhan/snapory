using System.Windows;
using System.Windows.Media.Imaging;
using snapory.Models;
using snapory.Services;

// Enabling WinForms (for the tray icon) pulls the System.Windows.Forms version
// of Application into scope too, so spell out the WPF one; also disambiguate from
// System.Windows.Localization.
using Application = System.Windows.Application;
using Localization = snapory.Services.Localization;

namespace snapory;

/// <summary>
/// Application entry point. Wires together the long-lived pieces of snapory and
/// runs it as a tray application: there is no window on startup, the app lives in
/// the system tray, and it only exits when the user chooses "Quit".
///
/// The core flow: press Ctrl+Shift+S → drag out a region of the screen → an
/// editor opens where it can be marked up and then copied or saved.
/// </summary>
public partial class App : Application
{
    private Mutex? _singleInstanceMutex;
    private SettingsStore _settings = null!;
    private HistoryStore _historyStore = null!;
    private CaptureHistory _history = null!;
    private HotkeyService _hotkey = null!;
    private TrayIcon _tray = null!;
    private RegionSelectOverlay? _overlay;
    private MainWindow? _mainWindow;
    private AboutWindow? _aboutWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Only one snapory should own the global hotkey at a time. If another
        // instance already holds the mutex, bow out quietly.
        _singleInstanceMutex = new Mutex(initiallyOwned: true,
            @"Local\snapory.SingleInstance", out var isFirstInstance);
        if (!isFirstInstance)
        {
            Shutdown();
            return;
        }

        base.OnStartup(e);

        // No visible window means closing a window must not end the app; shutdown
        // is driven explicitly from the tray's Quit command.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Apply the saved language and theme before any UI is built, then persist
        // any later change to either.
        _settings = new SettingsStore();
        Localization.Instance.Language = _settings.LoadLanguage();
        ThemeService.Apply(_settings.LoadTheme());
        Localization.Instance.LanguageChanged += SavePreferences;
        ThemeService.Changed += SavePreferences;

        // Restore the saved screenshot history.
        _historyStore = new HistoryStore();
        _history = new CaptureHistory(_historyStore);
        _history.Initialize();

        // Ctrl+Shift+S starts a capture from anywhere.
        _hotkey = new HotkeyService();
        _hotkey.Pressed += StartCapture;

        _tray = new TrayIcon();
        _tray.CaptureRequested += StartCapture;
        _tray.HistoryRequested += ShowMain;
        _tray.AboutRequested += ShowAbout;
        _tray.QuitRequested += Shutdown;

        // Launching with "--capture" starts a capture straight away; "--history"
        // (or "--open") opens the main window.
        if (e.Args.Contains("--capture"))
            StartCapture();
        else if (e.Args.Contains("--history") || e.Args.Contains("--open"))
            ShowMain();
    }

    /// <summary>Opens the region selector, unless one is already showing.</summary>
    private void StartCapture()
    {
        if (_overlay is not null)
            return;

        _overlay = new RegionSelectOverlay();
        _overlay.RegionSelected += OnRegionSelected;
        _overlay.Cancelled += () => _overlay = null;
        _overlay.Closed += (_, _) => _overlay = null;
        _overlay.Show();
    }

    private void OnRegionSelected(BitmapSource image)
    {
        // The overlay closes itself; show the captured region in the main window.
        ShowMain();
        _mainWindow!.ShowCapture(image);
    }

    private void SavePreferences()
        => _settings.Save(Localization.Instance.Language, ThemeService.Theme);

    /// <summary>Shows the main window (editor + history), reusing the single instance.</summary>
    private void ShowMain()
    {
        if (_mainWindow is null)
        {
            _mainWindow = new MainWindow(_history);
            _mainWindow.NewRequested += StartCapture;
            _mainWindow.AboutRequested += ShowAbout;
            // It hides on close and is reused; only drop it if truly closed.
            _mainWindow.Closed += (_, _) => _mainWindow = null;
        }

        Surface(_mainWindow);
    }

    // Make a (possibly hidden or minimized) window visible and frontmost.
    private static void Surface(Window window)
    {
        if (!window.IsVisible)
            window.Show();
        if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;
        window.Activate();
    }

    /// <summary>Shows the About window, reusing it if already open.</summary>
    private void ShowAbout()
    {
        if (_aboutWindow is not null)
        {
            _aboutWindow.Activate();
            return;
        }

        _aboutWindow = new AboutWindow();
        _aboutWindow.Closed += (_, _) => _aboutWindow = null;
        _aboutWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _hotkey?.Dispose();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
