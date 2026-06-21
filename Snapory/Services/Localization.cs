using System.ComponentModel;

namespace Snapory.Services;

public enum AppLanguage
{
    English,
    Turkish,
}

/// <summary>
/// The app's tiny translation table and current-language state.
///
/// UI elements bind to the string indexer (e.g. <c>[ToolArrow]</c>) against the
/// shared <see cref="Instance"/>. When <see cref="Language"/> changes we raise
/// the special "Item[]" property change so every bound string re-reads itself,
/// giving a live language switch without rebuilding the UI. Non-WPF consumers
/// (the tray menu) can instead listen to <see cref="LanguageChanged"/>.
/// </summary>
public sealed class Localization : INotifyPropertyChanged
{
    public static Localization Instance { get; } = new();

    private AppLanguage _language = AppLanguage.English;

    private static readonly Dictionary<string, string> English = new()
    {
        ["SelectHint"] = "Drag to select an area · Esc to cancel",
        ["TrayCapture"] = "New screenshot",
        ["TrayHistory"] = "Open history",
        ["HistoryTitle"] = "Snapory — History",
        ["HistoryEmpty"] = "No screenshots yet — press Ctrl + Shift + S to capture one",
        ["ClearAll"] = "Clear all",
        ["Delete"] = "Delete",
        ["Edit"] = "Edit",
        ["Open"] = "Open",
        ["TrayAutostart"] = "Start with Windows",
        ["TrayLanguage"] = "Language",
        ["TrayTheme"] = "Theme",
        ["ThemeSystem"] = "System",
        ["ThemeDark"] = "Dark",
        ["ThemeLight"] = "Light",
        ["TrayAbout"] = "About",
        ["TrayQuit"] = "Quit",
        ["EditorTitle"] = "Snapory — Edit",
        ["ToolArrow"] = "Arrow",
        ["ToolBox"] = "Box",
        ["ToolHighlight"] = "Highlight",
        ["ToolText"] = "Text",
        ["Undo"] = "Undo",
        ["Copy"] = "Copy",
        ["Save"] = "Save",
        ["Copied"] = "Copied to clipboard",
        ["AboutDescription"] = "A lightweight screenshot and annotation tool.",
        ["AboutVersion"] = "Version",
        ["AboutClose"] = "Close",
    };

    private static readonly Dictionary<string, string> Turkish = new()
    {
        ["SelectHint"] = "Bir alan seçmek için sürükle · Esc iptal",
        ["TrayCapture"] = "Yeni ekran görüntüsü",
        ["TrayHistory"] = "Geçmişi aç",
        ["HistoryTitle"] = "Snapory — Geçmiş",
        ["HistoryEmpty"] = "Henüz ekran görüntüsü yok — yakalamak için Ctrl + Shift + S",
        ["ClearAll"] = "Tümünü temizle",
        ["Delete"] = "Sil",
        ["Edit"] = "Düzenle",
        ["Open"] = "Aç",
        ["TrayAutostart"] = "Windows ile başlat",
        ["TrayLanguage"] = "Dil",
        ["TrayTheme"] = "Tema",
        ["ThemeSystem"] = "Sistem",
        ["ThemeDark"] = "Koyu",
        ["ThemeLight"] = "Açık",
        ["TrayAbout"] = "Hakkında",
        ["TrayQuit"] = "Çıkış",
        ["EditorTitle"] = "Snapory — Düzenle",
        ["ToolArrow"] = "Ok",
        ["ToolBox"] = "Kutu",
        ["ToolHighlight"] = "Vurgu",
        ["ToolText"] = "Yazı",
        ["Undo"] = "Geri al",
        ["Copy"] = "Kopyala",
        ["Save"] = "Kaydet",
        ["Copied"] = "Panoya kopyalandı",
        ["AboutDescription"] = "Hafif bir ekran görüntüsü ve işaretleme aracı.",
        ["AboutVersion"] = "Sürüm",
        ["AboutClose"] = "Kapat",
    };

    /// <summary>The active language. Changing it refreshes all bound strings.</summary>
    public AppLanguage Language
    {
        get => _language;
        set
        {
            if (_language == value)
                return;

            _language = value;

            // "Item[]" tells WPF that every indexer binding should re-evaluate.
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
            LanguageChanged?.Invoke();
        }
    }

    /// <summary>The translation for <paramref name="key"/> in the current language.</summary>
    public string this[string key]
    {
        get
        {
            var table = _language == AppLanguage.Turkish ? Turkish : English;
            return table.TryGetValue(key, out var value) ? value : key;
        }
    }

    /// <summary>Raised after the language changes (for non-binding consumers).</summary>
    public event Action? LanguageChanged;

    public event PropertyChangedEventHandler? PropertyChanged;
}
