using System.IO;
using System.Text.Json;

namespace Snapory.Services;

/// <summary>
/// Persists small user preferences (currently just the chosen language) as JSON
/// under %APPDATA%\Snapory. Best-effort: failures fall back to defaults rather
/// than throwing.
/// </summary>
public sealed class SettingsStore
{
    private sealed record Data(string Language);

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _filePath;

    public SettingsStore()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Snapory");
        Directory.CreateDirectory(folder);
        _filePath = Path.Combine(folder, "settings.json");
    }

    /// <summary>Loads the saved language, defaulting to English.</summary>
    public AppLanguage LoadLanguage()
    {
        try
        {
            if (!File.Exists(_filePath))
                return AppLanguage.English;

            var data = JsonSerializer.Deserialize<Data>(File.ReadAllText(_filePath));
            return data is not null && Enum.TryParse<AppLanguage>(data.Language, out var language)
                ? language
                : AppLanguage.English;
        }
        catch
        {
            return AppLanguage.English;
        }
    }

    /// <summary>Saves the chosen language.</summary>
    public void SaveLanguage(AppLanguage language)
    {
        try
        {
            File.WriteAllText(_filePath, JsonSerializer.Serialize(new Data(language.ToString()), JsonOptions));
        }
        catch
        {
            // Best-effort; a lost preference is not worth crashing over.
        }
    }
}
