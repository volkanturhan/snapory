using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Snapory.Services;

// WinForms is enabled for the tray, so disambiguate the types this window uses.
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingRectangle = System.Drawing.Rectangle;
using Point = System.Windows.Point;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Key = System.Windows.Input.Key;

namespace Snapory;

/// <summary>
/// The full-screen region selector. On show it freezes a snapshot of the whole
/// desktop, dims it, and lets the user drag out a rectangle; the area inside the
/// rectangle is shown at full brightness. Releasing the mouse raises
/// <see cref="RegionSelected"/> with the cropped image; Esc or right-click
/// cancels.
/// </summary>
public partial class RegionSelectOverlay : Window
{
    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);

    private readonly DrawingBitmap _snapshot;
    private readonly double _scale;

    private Point _start;
    private bool _dragging;
    private bool _finished;

    /// <summary>Raised with the cropped region once the user finishes selecting.</summary>
    public event Action<BitmapSource>? RegionSelected;

    /// <summary>Raised when the user cancels without selecting.</summary>
    public event Action? Cancelled;

    public RegionSelectOverlay()
    {
        InitializeComponent();

        _snapshot = ScreenCapture.CaptureVirtualScreen(out var bounds);
        var display = ToBitmapSource(_snapshot);
        ScreenImage.Source = display;
        BrightImage.Source = display;

        // Cover the whole virtual desktop. Window coordinates are
        // device-independent, so convert the physical bounds by the system DPI
        // scale (correct for the common uniform-scaling setup).
        _scale = GetDpiForSystem() / 96.0;
        Left = bounds.Left / _scale;
        Top = bounds.Top / _scale;
        Width = bounds.Width / _scale;
        Height = bounds.Height / _scale;

        Loaded += OnLoaded;
        MouseLeftButtonDown += OnDown;
        MouseMove += OnMove;
        MouseLeftButtonUp += OnUp;
        MouseRightButtonDown += (_, _) => Cancel();
        KeyDown += OnKeyDown;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        WindowActivator.ForceToForeground(new WindowInteropHelper(this).Handle);
        Activate();
        Focus();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            Cancel();
    }

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        _start = e.GetPosition(Root);
        _dragging = true;
        CaptureMouse();

        SelectionRect.Visibility = Visibility.Visible;
        BrightImage.Visibility = Visibility.Visible;
        SizeBadge.Visibility = Visibility.Visible;
        UpdateSelection(_start);
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (_dragging)
            UpdateSelection(e.GetPosition(Root));
    }

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging)
            return;

        _dragging = false;
        if (IsMouseCaptured)
            ReleaseMouseCapture();

        var rect = NormalizedRect(e.GetPosition(Root));

        // A tiny rectangle is almost certainly a stray click, not a selection.
        if (rect.Width < 4 || rect.Height < 4)
        {
            Cancel();
            return;
        }

        if (TryCrop(rect, out var cropped))
        {
            _finished = true;
            var source = ToBitmapSource(cropped);
            cropped.Dispose();
            RegionSelected?.Invoke(source);
            Close();
        }
        else
        {
            Cancel();
        }
    }

    // Move/resize the selection outline, punch the bright window through the dim
    // veil, and update the size readout.
    private void UpdateSelection(Point current)
    {
        var rect = NormalizedRect(current);

        Canvas.SetLeft(SelectionRect, rect.X);
        Canvas.SetTop(SelectionRect, rect.Y);
        SelectionRect.Width = rect.Width;
        SelectionRect.Height = rect.Height;

        BrightImage.Clip = new RectangleGeometry(rect);

        SizeText.Text = $"{Math.Round(rect.Width * _scale)} × {Math.Round(rect.Height * _scale)}";
        // Keep the badge just above the selection, or below it near the top edge.
        var badgeY = rect.Y - 26 > 0 ? rect.Y - 26 : rect.Y + 6;
        Canvas.SetLeft(SizeBadge, rect.X);
        Canvas.SetTop(SizeBadge, badgeY);
    }

    private Rect NormalizedRect(Point current)
    {
        var x = Math.Min(_start.X, current.X);
        var y = Math.Min(_start.Y, current.Y);
        var w = Math.Abs(current.X - _start.X);
        var h = Math.Abs(current.Y - _start.Y);
        return new Rect(x, y, w, h);
    }

    // Crop the physical snapshot to the selection (converting DIP -> pixels).
    private bool TryCrop(Rect dipRect, out DrawingBitmap cropped)
    {
        cropped = null!;

        var x = (int)Math.Round(dipRect.X * _scale);
        var y = (int)Math.Round(dipRect.Y * _scale);
        var w = (int)Math.Round(dipRect.Width * _scale);
        var h = (int)Math.Round(dipRect.Height * _scale);

        x = Math.Clamp(x, 0, _snapshot.Width - 1);
        y = Math.Clamp(y, 0, _snapshot.Height - 1);
        w = Math.Clamp(w, 1, _snapshot.Width - x);
        h = Math.Clamp(h, 1, _snapshot.Height - y);

        cropped = _snapshot.Clone(new DrawingRectangle(x, y, w, h), _snapshot.PixelFormat);
        return true;
    }

    private void Cancel()
    {
        if (_finished)
            return;

        _finished = true;
        if (IsMouseCaptured)
            ReleaseMouseCapture();
        Cancelled?.Invoke();
        Close();
    }

    private static BitmapSource ToBitmapSource(DrawingBitmap bitmap)
    {
        var handle = bitmap.GetHbitmap();
        try
        {
            var source = Imaging.CreateBitmapSourceFromHBitmap(
                handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            DeleteObject(handle);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _snapshot.Dispose();
        base.OnClosed(e);
    }
}
