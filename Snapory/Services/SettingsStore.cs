using System.IO;
using System.Text.Json;

namespace Snapory.Services;

/// <summary>
/// Persists small user preferences — the chosen language and colour theme — as
/// JSON under %APPDATA%\Snapory. Best-effort: failures fall back to defaults
/// rather than throwing.
/// </summary>
public sealed class SettingsStore
{
    private sealed record Data(string Language, string Theme);

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
        var data = Read();
        return data is not null && Enum.TryParse<AppLanguage>(data.Language, out var language)
            ? language
            : AppLanguage.English;
    }

    /// <summary>Loads the saved theme, defaulting to System (follow Windows).</summary>
    public AppTheme LoadTheme()
    {
        var data = Read();
        return data is not null && Enum.TryParse<AppTheme>(data.Theme, out var theme)
            ? theme
            : AppTheme.System;
    }

    /// <summary>Saves both preferences together.</summary>
    public void Save(AppLanguage language, AppTheme theme)
    {
        try
        {
            var data = new Data(language.ToString(), theme.ToString());
            File.WriteAllText(_filePath, JsonSerializer.Serialize(data, JsonOptions));
        }
        catch
        {
            // Best-effort; a lost preference is not worth crashing over.
        }
    }

    private Data? Read()
    {
        try
        {
            return File.Exists(_filePath)
                ? JsonSerializer.Deserialize<Data>(File.ReadAllText(_filePath))
                : null;
        }
        catch
        {
            return null;
        }
    }
}
