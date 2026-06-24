using System.Drawing;
using System.Windows.Forms;

namespace snapory.Services;

/// <summary>
/// The system-tray presence for snapory. While the app runs it lives here rather
/// than on the taskbar. The context menu starts a new capture and exposes the
/// usual settings; the events below let the application decide what each one does.
///
/// Menu text follows the app language: the menu is built once and its labels are
/// refreshed whenever <see cref="Localization"/> changes. Backed by the WinForms
/// <see cref="NotifyIcon"/>, which ships with the .NET SDK so snapory needs no
/// third-party tray library.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly Icon? _icon;

    // Hidden until an update is found; shown bold at the top of the menu, with
    // its own separator, so it stands out without cluttering the normal menu.
    private readonly ToolStripMenuItem _updateItem = new() { Visible = false };
    private readonly ToolStripSeparator _updateSeparator = new() { Visible = false };
    private string? _updateVersion;

    private readonly ToolStripMenuItem _captureItem = new();
    private readonly ToolStripMenuItem _historyItem = new();
    private readonly ToolStripMenuItem _autoStartItem = new() { CheckOnClick = true };
    private readonly ToolStripMenuItem _languageItem = new();
    private readonly ToolStripMenuItem _englishItem = new("English");
    private readonly ToolStripMenuItem _turkishItem = new("Türkçe");
    private readonly ToolStripMenuItem _checkUpdateItem = new();
    private readonly ToolStripMenuItem _aboutItem = new();
    private readonly ToolStripMenuItem _quitItem = new();

    /// <summary>Raised when the user asks to start a new screenshot.</summary>
    public event Action? CaptureRequested;

    /// <summary>Raised when the user asks to open the history gallery.</summary>
    public event Action? HistoryRequested;

    /// <summary>Raised when the user asks to see the About window.</summary>
    public event Action? AboutRequested;

    /// <summary>Raised when the user asks to quit the application.</summary>
    public event Action? QuitRequested;

    /// <summary>Raised when the user accepts the offered update.</summary>
    public event Action? UpdateRequested;

    /// <summary>Raised when the user asks to check for updates now.</summary>
    public event Action? CheckUpdateRequested;

    public TrayIcon()
    {
        // The update entry is drawn bold to read as the call-to-action it is.
        _updateItem.Font = new Font(SystemFonts.MenuFont!, System.Drawing.FontStyle.Bold);
        _updateItem.Click += (_, _) => UpdateRequested?.Invoke();

        _captureItem.Click += (_, _) => CaptureRequested?.Invoke();
        _historyItem.Click += (_, _) => HistoryRequested?.Invoke();
        _autoStartItem.Checked = AutoStart.IsEnabled();
        _autoStartItem.CheckedChanged += (_, _) => AutoStart.SetEnabled(_autoStartItem.Checked);
        _aboutItem.Click += (_, _) => AboutRequested?.Invoke();
        _quitItem.Click += (_, _) => QuitRequested?.Invoke();

        _englishItem.Click += (_, _) => Localization.Instance.Language = AppLanguage.English;
        _turkishItem.Click += (_, _) => Localization.Instance.Language = AppLanguage.Turkish;
        _languageItem.DropDownItems.Add(_englishItem);
        _languageItem.DropDownItems.Add(_turkishItem);

        _checkUpdateItem.Click += (_, _) => CheckUpdateRequested?.Invoke();

        var menu = new ContextMenuStrip();
        menu.Items.AddRange(new ToolStripItem[]
        {
            _updateItem,
            _updateSeparator,
            _captureItem,
            _historyItem,
            new ToolStripSeparator(),
            _autoStartItem,
            _languageItem,
            _checkUpdateItem,
            _aboutItem,
            new ToolStripSeparator(),
            _quitItem,
        });

        // Capturing is the headline command, so make it the default (bold) item
        // and the double-click behaviour.
        _captureItem.Font = new Font(menu.Font, System.Drawing.FontStyle.Bold);

        _icon = TryLoadAppIcon();
        _notifyIcon = new NotifyIcon
        {
            // Fall back to a generic icon if ours fails to load — never crash the
            // whole app over a tray icon.
            Icon = _icon ?? SystemIcons.Application,
            Text = "snapory",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _notifyIcon.DoubleClick += (_, _) => HistoryRequested?.Invoke();
        // We only ever raise a balloon for an update, so clicking it means "yes".
        _notifyIcon.BalloonTipClicked += (_, _) => UpdateRequested?.Invoke();

        Localization.Instance.LanguageChanged += ApplyLanguage;
        ApplyLanguage();
    }

    // Refresh every menu label from the current language and tick the active
    // language entry.
    private void ApplyLanguage()
    {
        var text = Localization.Instance;
        _captureItem.Text = text["TrayCapture"];
        _historyItem.Text = text["TrayHistory"];
        _autoStartItem.Text = text["TrayAutostart"];
        _languageItem.Text = text["TrayLanguage"];
        _checkUpdateItem.Text = text["TrayCheckUpdate"];
        _aboutItem.Text = text["TrayAbout"];
        _quitItem.Text = text["TrayQuit"];

        // Keep the (version-stamped) update label in the current language too.
        if (_updateVersion is not null)
            _updateItem.Text = string.Format(text["TrayUpdate"], _updateVersion);

        _englishItem.Checked = text.Language == AppLanguage.English;
        _turkishItem.Checked = text.Language == AppLanguage.Turkish;
    }

    /// <summary>
    /// Reveals the update entry for <paramref name="version"/> and shows a tray
    /// balloon so the user notices even without opening the menu. Call on the UI
    /// thread once a newer release has been found.
    /// </summary>
    public void ShowUpdateAvailable(string version)
    {
        _updateVersion = version;
        _updateItem.Visible = true;
        _updateSeparator.Visible = true;
        ApplyLanguage();

        var text = Localization.Instance;
        _notifyIcon.BalloonTipTitle = text["UpdateBalloonTitle"];
        _notifyIcon.BalloonTipText = text["UpdateBalloonText"];
        _notifyIcon.ShowBalloonTip(5000);
    }

    /// <summary>
    /// Shows a brief "you're up to date" balloon. Used to give feedback when the
    /// user checks for updates manually and there is nothing newer.
    /// </summary>
    public void ShowUpToDate()
    {
        var text = Localization.Instance;
        _notifyIcon.BalloonTipTitle = text["UpdateBalloonTitle"];
        _notifyIcon.BalloonTipText = text["UpToDate"];
        _notifyIcon.ShowBalloonTip(4000);
    }

    /// <summary>
    /// Loads the bundled snapory icon at the system's small-icon size so the tray
    /// gets a crisp frame. Returns null on any failure.
    /// </summary>
    private static Icon? TryLoadAppIcon()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Assets/snapory.ico");
            using var stream = System.Windows.Application.GetResourceStream(uri).Stream;
            return new Icon(stream, SystemInformation.SmallIconSize);
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        Localization.Instance.LanguageChanged -= ApplyLanguage;

        // Hide before disposing so the icon disappears immediately instead of
        // lingering in the tray until the user hovers over it.
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _icon?.Dispose();
    }
}
