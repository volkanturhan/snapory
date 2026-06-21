using System.Windows;
using System.Windows.Media.Imaging;
using Snapory.Models;
using Snapory.Services;

// Enabling WinForms (for the tray icon) pulls the System.Windows.Forms version
// of Application into scope too, so spell out the WPF one; also disambiguate from
// System.Windows.Localization.
using Application = System.Windows.Application;
using Localization = Snapory.Services.Localization;

namespace Snapory;

/// <summary>
/// Application entry point. Wires together the long-lived pieces of Snapory and
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
    private HistoryWindow? _historyWindow;
    private AboutWindow? _aboutWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Only one Snapory should own the global hotkey at a time. If another
        // instance already holds the mutex, bow out quietly.
        _singleInstanceMutex = new Mutex(initiallyOwned: true,
            @"Local\Snapory.SingleInstance", out var isFirstInstance);
        if (!isFirstInstance)
        {
            Shutdown();
            return;
        }

        base.OnStartup(e);

        // No visible window means closing a window must not end the app; shutdown
        // is driven explicitly from the tray's Quit command.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Apply the saved language before any UI is built, then persist changes.
        _settings = new SettingsStore();
        Localization.Instance.Language = _settings.LoadLanguage();
        Localization.Instance.LanguageChanged +=
            () => _settings.SaveLanguage(Localization.Instance.Language);

        // Restore the saved screenshot history.
        _historyStore = new HistoryStore();
        _history = new CaptureHistory(_historyStore);
        _history.Initialize();

        // Ctrl+Shift+S starts a capture from anywhere.
        _hotkey = new HotkeyService();
        _hotkey.Pressed += StartCapture;

        _tray = new TrayIcon();
        _tray.CaptureRequested += StartCapture;
        _tray.HistoryRequested += ShowHistory;
        _tray.AboutRequested += ShowAbout;
        _tray.QuitRequested += Shutdown;

        // Launching with "--capture" starts a capture straight away; "--history"
        // opens the gallery.
        if (e.Args.Contains("--capture"))
            StartCapture();
        else if (e.Args.Contains("--history"))
            ShowHistory();
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
        // The overlay closes itself; open an editor for the captured region.
        OpenEditor(image);
    }

    private void OpenEditor(BitmapSource image)
    {
        var editor = new EditorWindow(image, _history);
        editor.Show();
        editor.Activate();
    }

    /// <summary>Shows the history gallery, reusing it if already open.</summary>
    private void ShowHistory()
    {
        if (_historyWindow is not null)
        {
            _historyWindow.Activate();
            return;
        }

        _historyWindow = new HistoryWindow(_history);
        _historyWindow.NewRequested += StartCapture;
        _historyWindow.OpenRequested += shot => OpenEditor(shot.LoadFull());
        _historyWindow.AboutRequested += ShowAbout;
        _historyWindow.Closed += (_, _) => _historyWindow = null;
        _historyWindow.Show();
        _historyWindow.Activate();
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
