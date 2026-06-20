using System.Drawing;
using System.Windows.Forms;

namespace Snapory.Services;

/// <summary>
/// The system-tray presence for Snapory. While the app runs it lives here rather
/// than on the taskbar. The context menu starts a new capture and exposes the
/// usual settings; the events below let the application decide what each one does.
///
/// Menu text follows the app language: the menu is built once and its labels are
/// refreshed whenever <see cref="Localization"/> changes. Backed by the WinForms
/// <see cref="NotifyIcon"/>, which ships with the .NET SDK so Snapory needs no
/// third-party tray library.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly Icon? _icon;

    private readonly ToolStripMenuItem _captureItem = new();
    private readonly ToolStripMenuItem _autoStartItem = new() { CheckOnClick = true };
    private readonly ToolStripMenuItem _languageItem = new();
    private readonly ToolStripMenuItem _englishItem = new("English");
    private readonly ToolStripMenuItem _turkishItem = new("Türkçe");
    private readonly ToolStripMenuItem _aboutItem = new();
    private readonly ToolStripMenuItem _quitItem = new();

    /// <summary>Raised when the user asks to start a new screenshot.</summary>
    public event Action? CaptureRequested;

    /// <summary>Raised when the user asks to see the About window.</summary>
    public event Action? AboutRequested;

    /// <summary>Raised when the user asks to quit the application.</summary>
    public event Action? QuitRequested;

    public TrayIcon()
    {
        _captureItem.Click += (_, _) => CaptureRequested?.Invoke();
        _autoStartItem.Checked = AutoStart.IsEnabled();
        _autoStartItem.CheckedChanged += (_, _) => AutoStart.SetEnabled(_autoStartItem.Checked);
        _aboutItem.Click += (_, _) => AboutRequested?.Invoke();
        _quitItem.Click += (_, _) => QuitRequested?.Invoke();

        _englishItem.Click += (_, _) => Localization.Instance.Language = AppLanguage.English;
        _turkishItem.Click += (_, _) => Localization.Instance.Language = AppLanguage.Turkish;
        _languageItem.DropDownItems.Add(_englishItem);
        _languageItem.DropDownItems.Add(_turkishItem);

        var menu = new ContextMenuStrip();
        menu.Items.AddRange(new ToolStripItem[]
        {
            _captureItem,
            new ToolStripSeparator(),
            _autoStartItem,
            _languageItem,
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
            Text = "Snapory",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _notifyIcon.DoubleClick += (_, _) => CaptureRequested?.Invoke();

        Localization.Instance.LanguageChanged += ApplyLanguage;
        ApplyLanguage();
    }

    // Refresh every menu label from the current language and tick the active
    // language entry.
    private void ApplyLanguage()
    {
        var text = Localization.Instance;
        _captureItem.Text = text["TrayCapture"];
        _autoStartItem.Text = text["TrayAutostart"];
        _languageItem.Text = text["TrayLanguage"];
        _aboutItem.Text = text["TrayAbout"];
        _quitItem.Text = text["TrayQuit"];

        _englishItem.Checked = text.Language == AppLanguage.English;
        _turkishItem.Checked = text.Language == AppLanguage.Turkish;
    }

    /// <summary>
    /// Loads the bundled Snapory icon at the system's small-icon size so the tray
    /// gets a crisp frame. Returns null on any failure.
    /// </summary>
    private static Icon? TryLoadAppIcon()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Assets/Snapory.ico");
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
