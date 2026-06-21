using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using snapory.Models;

namespace snapory.Services;

/// <summary>
/// The in-memory list of saved screenshots, newest first, backed by
/// <see cref="HistoryStore"/> on disk. The gallery binds to <see cref="Items"/>;
/// every change is persisted and raises <see cref="Changed"/>.
/// </summary>
public sealed class CaptureHistory
{
    /// <summary>Maximum number of shots kept before the oldest is dropped.</summary>
    public const int Capacity = 60;

    private readonly ObservableCollection<Shot> _items = new();
    private readonly HistoryStore _store;

    public CaptureHistory(HistoryStore store)
    {
        _store = store;
        Items = new ReadOnlyObservableCollection<Shot>(_items);
    }

    /// <summary>The shots, newest first, exposed read-only for binding.</summary>
    public ReadOnlyObservableCollection<Shot> Items { get; }

    /// <summary>Raised after any change (add, update, remove, clear).</summary>
    public event Action? Changed;

    /// <summary>Loads the saved shots from disk.</summary>
    public void Initialize()
    {
        _items.Clear();
        foreach (var shot in _store.Load())
            _items.Add(shot);

        Changed?.Invoke();
    }

    /// <summary>Saves a new screenshot to the top of the history.</summary>
    public Shot Add(BitmapSource image)
    {
        var shot = _store.SaveNew(image, DateTime.Now);
        _items.Insert(0, shot);
        Trim();
        _store.SaveIndex(_items);
        Changed?.Invoke();
        return shot;
    }

    /// <summary>Re-saves an existing shot with an updated image (same slot).</summary>
    public void Update(Shot shot, BitmapSource image)
    {
        _store.Overwrite(shot, image);
        shot.RefreshThumbnail();
        Changed?.Invoke();
    }

    /// <summary>Deletes a single shot.</summary>
    public void Remove(Shot shot)
    {
        if (!_items.Remove(shot))
            return;

        _store.Delete(shot);
        _store.SaveIndex(_items);
        Changed?.Invoke();
    }

    /// <summary>Deletes every shot.</summary>
    public void Clear()
    {
        foreach (var shot in _items)
            _store.Delete(shot);

        _items.Clear();
        _store.SaveIndex(_items);
        Changed?.Invoke();
    }

    // Drop the oldest shots (and their files) beyond the capacity.
    private void Trim()
    {
        while (_items.Count > Capacity)
        {
            var oldest = _items[^1];
            _items.RemoveAt(_items.Count - 1);
            _store.Delete(oldest);
        }
    }
}
