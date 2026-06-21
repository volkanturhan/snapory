using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;
using snapory.Models;

namespace snapory.Services;

/// <summary>
/// Persists the screenshot history: each shot is a PNG under
/// %APPDATA%\snapory\history, with an index.json listing them. All operations are
/// best-effort — a missing or corrupt index simply yields an empty history, and a
/// failed write is swallowed rather than allowed to crash the app.
/// </summary>
public sealed class HistoryStore
{
    private sealed record Entry(string File, long CapturedAtTicks);

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _folder;
    private readonly string _indexPath;

    public HistoryStore()
    {
        _folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "snapory", "history");
        Directory.CreateDirectory(_folder);
        _indexPath = Path.Combine(_folder, "index.json");
    }

    /// <summary>Loads the saved shots, newest first, skipping any whose file is gone.</summary>
    public IReadOnlyList<Shot> Load()
    {
        try
        {
            if (!File.Exists(_indexPath))
                return Array.Empty<Shot>();

            var entries = JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(_indexPath))
                ?? new List<Entry>();

            return entries
                .Select(e => (path: Path.Combine(_folder, e.File), e.CapturedAtTicks))
                .Where(x => File.Exists(x.path))
                .Select(x => new Shot(x.path, new DateTime(x.CapturedAtTicks)))
                .OrderByDescending(s => s.CapturedAt)
                .ToList();
        }
        catch
        {
            return Array.Empty<Shot>();
        }
    }

    /// <summary>Writes a new PNG for <paramref name="image"/> and returns its shot.</summary>
    public Shot SaveNew(BitmapSource image, DateTime capturedAt)
    {
        var file = $"{capturedAt:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.png";
        var path = Path.Combine(_folder, file);
        Encode(image, path);
        return new Shot(path, capturedAt);
    }

    /// <summary>Re-encodes an existing shot's file with an updated image.</summary>
    public void Overwrite(Shot shot, BitmapSource image)
    {
        try
        {
            Encode(image, shot.FilePath);
        }
        catch
        {
            // Best-effort.
        }
    }

    /// <summary>Deletes a single shot's file from disk.</summary>
    public void Delete(Shot shot)
    {
        try
        {
            if (File.Exists(shot.FilePath))
                File.Delete(shot.FilePath);
        }
        catch
        {
            // Best-effort.
        }
    }

    /// <summary>Persists the current set of shots to the index.</summary>
    public void SaveIndex(IEnumerable<Shot> shots)
    {
        try
        {
            var entries = shots
                .Select(s => new Entry(Path.GetFileName(s.FilePath), s.CapturedAt.Ticks))
                .ToList();
            File.WriteAllText(_indexPath, JsonSerializer.Serialize(entries, JsonOptions));
        }
        catch
        {
            // Best-effort.
        }
    }

    private static void Encode(BitmapSource image, string path)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
