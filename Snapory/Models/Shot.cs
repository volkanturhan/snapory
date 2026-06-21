using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace Snapory.Models;

/// <summary>
/// One saved screenshot in the history: the PNG file it lives in on disk plus a
/// small, cached thumbnail for the gallery. The full image is loaded on demand
/// (e.g. to reopen it in the editor).
/// </summary>
public sealed class Shot : INotifyPropertyChanged
{
    // Thumbnails are decoded at this width to keep the gallery light.
    private const int ThumbnailWidth = 240;

    private BitmapSource? _thumbnail;

    public Shot(string filePath, DateTime capturedAt)
    {
        FilePath = filePath;
        CapturedAt = capturedAt;
    }

    /// <summary>Full path to the PNG on disk.</summary>
    public string FilePath { get; }

    /// <summary>When the screenshot was taken.</summary>
    public DateTime CapturedAt { get; }

    /// <summary>A human-friendly capture time for the gallery caption.</summary>
    public string Caption => CapturedAt.ToString("d MMM HH:mm");

    /// <summary>A small cached thumbnail, decoded on first access.</summary>
    public BitmapSource Thumbnail => _thumbnail ??= Load(ThumbnailWidth);

    /// <summary>Loads the full-resolution image (e.g. to reopen in the editor).</summary>
    public BitmapSource LoadFull() => Load(0);

    /// <summary>Drops the cached thumbnail so it is re-read after the file changes.</summary>
    public void RefreshThumbnail()
    {
        _thumbnail = null;
        OnPropertyChanged(nameof(Thumbnail));
    }

    // Decode the file fully into memory (OnLoad) so it is not left locked on
    // disk — that way the history can still delete it later.
    private BitmapSource Load(int decodeWidth)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
        if (decodeWidth > 0)
            image.DecodePixelWidth = decodeWidth;
        image.UriSource = new Uri(FilePath);
        image.EndInit();
        image.Freeze();
        return image;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
