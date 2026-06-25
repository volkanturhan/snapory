using System.IO;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using snapory.Models;
using snapory.Services;

// WinForms is enabled for the tray, so disambiguate everything this window uses
// down to its WPF type.
using Localization = snapory.Services.Localization;
using TextBox = System.Windows.Controls.TextBox;
using Rectangle = System.Windows.Shapes.Rectangle;
using Path = System.Windows.Shapes.Path;
using Canvas = System.Windows.Controls.Canvas;
using Color = System.Windows.Media.Color;
using Brush = System.Windows.Media.Brush;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using Point = System.Windows.Point;
using Clipboard = System.Windows.Clipboard;
using SelectionChangedEventArgs = System.Windows.Controls.SelectionChangedEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Key = System.Windows.Input.Key;
using Keyboard = System.Windows.Input.Keyboard;
using ModifierKeys = System.Windows.Input.ModifierKeys;

namespace snapory;

/// <summary>
/// snapory's single main window: the editing canvas (with the drawing toolbar) on
/// the left, and the screenshot history down the right. A new capture is added to
/// the history and loaded into the canvas; clicking a thumbnail loads it for
/// editing. Copy/Save flatten the canvas and update that shot.
/// </summary>
public partial class MainWindow : Window
{
    private enum Tool { Arrow, Box, Highlight, Text }

    private static readonly Color[] Swatches =
    {
        Color.FromRgb(0xEF, 0x44, 0x44), // red
        Color.FromRgb(0xF9, 0x73, 0x16), // orange
        Color.FromRgb(0xFA, 0xCC, 0x15), // yellow
        Color.FromRgb(0x22, 0xC5, 0x5E), // green
        Color.FromRgb(0x3B, 0x82, 0xF6), // blue
        Color.FromRgb(0xFF, 0xFF, 0xFF), // white
        Color.FromRgb(0x11, 0x18, 0x27), // near-black
    };

    private const double StrokeWidth = 3;
    private static readonly Brush TransparentBrush = Frozen(Colors.Transparent);

    private readonly CaptureHistory _history;
    private readonly List<UIElement> _undo = new();

    private Tool _tool = Tool.Arrow;
    private Color _color = Swatches[0];
    private UIElement? _active;
    private Point _start;

    private Shot? _currentShot;
    private int _pixelWidth;
    private int _pixelHeight;

    /// <summary>Raised when the user asks to start a new capture.</summary>
    public event Action? NewRequested;

    public MainWindow(CaptureHistory history)
    {
        InitializeComponent();

        _history = history;
        ShotList.ItemsSource = history.Items;

        BuildSwatches();
        ArrowTool.IsChecked = true;
        PreviewKeyDown += OnPreviewKeyDown;

        if (history.Items.Count > 0)
            ShotList.SelectedIndex = 0;

        UpdateState();
    }

    /// <summary>Adds a freshly captured image to the history and opens it for editing.</summary>
    public void ShowCapture(BitmapSource image)
    {
        var shot = _history.Add(image);
        ShotList.SelectedItem = shot;       // triggers LoadShot via selection
        ShotList.ScrollIntoView(shot);
    }

    // --- history list ------------------------------------------------------

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ShotList.SelectedItem is Shot shot)
            LoadShot(shot);
        else
            ClearCanvas();

        UpdateState();
    }

    private void LoadShot(Shot shot)
    {
        _currentShot = shot;
        Annotations.Children.Clear();
        _undo.Clear();
        _active = null;

        var image = shot.LoadFull();
        _pixelWidth = image.PixelWidth;
        _pixelHeight = image.PixelHeight;
        ShotImage.Source = image;
        ShotImage.Width = _pixelWidth;
        ShotImage.Height = _pixelHeight;
        Annotations.Width = _pixelWidth;
        Annotations.Height = _pixelHeight;
    }

    private void ClearCanvas()
    {
        _currentShot = null;
        Annotations.Children.Clear();
        _undo.Clear();
        _active = null;
        ShotImage.Source = null;
    }

    private void OnDelete(object sender, RoutedEventArgs e)
    {
        if (ShotList.SelectedItem is not Shot shot)
            return;

        var index = ShotList.SelectedIndex;
        _history.Remove(shot);

        if (ShotList.Items.Count > 0)
            ShotList.SelectedIndex = Math.Min(index, ShotList.Items.Count - 1);
        else
            ClearCanvas();

        UpdateState();
    }

    private void OnClearAll(object sender, RoutedEventArgs e)
    {
        _history.Clear();
        ClearCanvas();
        UpdateState();
    }

    private void OnNew(object sender, RoutedEventArgs e) => NewRequested?.Invoke();

    // --- toolbar -----------------------------------------------------------

    private void OnToolClick(object sender, RoutedEventArgs e)
    {
        _tool = sender switch
        {
            _ when ReferenceEquals(sender, BoxTool) => Tool.Box,
            _ when ReferenceEquals(sender, HighlightTool) => Tool.Highlight,
            _ when ReferenceEquals(sender, TextTool) => Tool.Text,
            _ => Tool.Arrow,
        };

        ArrowTool.IsChecked = _tool == Tool.Arrow;
        BoxTool.IsChecked = _tool == Tool.Box;
        HighlightTool.IsChecked = _tool == Tool.Highlight;
        TextTool.IsChecked = _tool == Tool.Text;
    }

    private void BuildSwatches()
    {
        foreach (var color in Swatches)
        {
            var toggle = new ToggleButton
            {
                Style = (Style)FindResource("SwatchToggle"),
                Background = Frozen(color),
                Tag = color,
                IsChecked = color == _color,
            };
            toggle.Click += OnSwatchClick;
            SwatchPanel.Children.Add(toggle);
        }
    }

    private void OnSwatchClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton clicked || clicked.Tag is not Color color)
            return;

        _color = color;
        foreach (var child in SwatchPanel.Children)
        {
            if (child is ToggleButton t)
                t.IsChecked = ReferenceEquals(t, clicked);
        }
    }

    // --- drawing -----------------------------------------------------------

    private void OnCanvasDown(object sender, MouseButtonEventArgs e)
    {
        if (ShotImage.Source is null)
            return;

        var pt = e.GetPosition(Annotations);

        if (_tool == Tool.Text)
        {
            AddTextBox(pt);
            UpdateState();
            return;
        }

        _start = pt;
        _active = _tool switch
        {
            Tool.Box => NewBox(),
            Tool.Highlight => NewHighlight(),
            _ => NewArrow(),
        };

        Annotations.Children.Add(_active);
        _undo.Add(_active);
        Annotations.CaptureMouse();
        UpdateState();
    }

    private void OnCanvasMove(object sender, MouseEventArgs e)
    {
        if (_active is null)
            return;

        var pt = e.GetPosition(Annotations);
        switch (_active)
        {
            case Path arrow:
                arrow.Data = BuildArrow(_start, pt);
                break;
            case Rectangle rect:
                LayoutRect(rect, _start, pt);
                break;
        }
    }

    private void OnCanvasUp(object sender, MouseButtonEventArgs e)
    {
        if (_active is null)
            return;

        if (_active is Rectangle { Width: < 3, Height: < 3 } stray)
        {
            Annotations.Children.Remove(stray);
            _undo.Remove(stray);
        }

        _active = null;
        if (Annotations.IsMouseCaptured)
            Annotations.ReleaseMouseCapture();
        UpdateState();
    }

    private Path NewArrow() => new()
    {
        Stroke = Frozen(_color),
        StrokeThickness = StrokeWidth,
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round,
        StrokeLineJoin = PenLineJoin.Round,
    };

    private Rectangle NewBox() => new()
    {
        Stroke = Frozen(_color),
        StrokeThickness = StrokeWidth,
        Fill = TransparentBrush,
    };

    private Rectangle NewHighlight() => new()
    {
        Fill = Frozen(Color.FromArgb(0x66, _color.R, _color.G, _color.B)),
    };

    private void AddTextBox(Point at)
    {
        var box = new TextBox
        {
            Background = TransparentBrush,
            BorderThickness = new Thickness(0),
            Foreground = Frozen(_color),
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
            MinWidth = 24,
            AcceptsReturn = false,
            TextWrapping = System.Windows.TextWrapping.NoWrap,
        };
        Canvas.SetLeft(box, at.X);
        Canvas.SetTop(box, at.Y);

        Annotations.Children.Add(box);
        _undo.Add(box);
        Dispatcher.BeginInvoke(() => box.Focus());
    }

    private static void LayoutRect(Rectangle rect, Point a, Point b)
    {
        Canvas.SetLeft(rect, Math.Min(a.X, b.X));
        Canvas.SetTop(rect, Math.Min(a.Y, b.Y));
        rect.Width = Math.Abs(b.X - a.X);
        rect.Height = Math.Abs(b.Y - a.Y);
    }

    private static Geometry BuildArrow(Point start, Point end)
    {
        var geometry = new StreamGeometry();
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);

        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(start, false, false);
            ctx.LineTo(end, true, true);

            if (length >= 1)
            {
                var ux = dx / length;
                var uy = dy / length;
                var head = Math.Min(18, length * 0.4);
                const double angle = 28 * Math.PI / 180;
                var cos = Math.Cos(angle);
                var sin = Math.Sin(angle);

                var leftX = end.X - head * (ux * cos - uy * sin);
                var leftY = end.Y - head * (ux * sin + uy * cos);
                var rightX = end.X - head * (ux * cos + uy * sin);
                var rightY = end.Y - head * (-ux * sin + uy * cos);

                ctx.BeginFigure(end, false, false);
                ctx.LineTo(new Point(leftX, leftY), true, true);
                ctx.BeginFigure(end, false, false);
                ctx.LineTo(new Point(rightX, rightY), true, true);
            }
        }

        geometry.Freeze();
        return geometry;
    }

    // --- commands ----------------------------------------------------------

    private void OnUndo(object sender, RoutedEventArgs e)
    {
        if (_undo.Count == 0)
            return;

        var last = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        Annotations.Children.Remove(last);
        UpdateState();
    }

    private void OnCopy(object sender, RoutedEventArgs e)
    {
        if (ShotImage.Source is null)
            return;

        var image = RenderFlattened();
        try
        {
            Clipboard.SetImage(image);
        }
        catch
        {
            // Ignore a transient clipboard failure rather than crash.
        }

        SaveToHistory(image);
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (ShotImage.Source is null)
            return;

        var dialog = new System.Windows.Forms.SaveFileDialog
        {
            Filter = "PNG image|*.png",
            FileName = $"snapory-{DateTime.Now:yyyyMMdd-HHmmss}.png",
        };

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            return;

        var image = RenderFlattened();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using (var stream = File.Create(dialog.FileName))
            encoder.Save(stream);

        SaveToHistory(image);
    }

    // Persist the flattened result back into the current history slot.
    private void SaveToHistory(BitmapSource image)
    {
        if (_currentShot is not null)
            _history.Update(_currentShot, image);
    }

    private BitmapSource RenderFlattened()
    {
        Keyboard.ClearFocus();
        EditSurface.UpdateLayout();

        var bitmap = new RenderTargetBitmap(_pixelWidth, _pixelHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(EditSurface);
        bitmap.Freeze();
        return bitmap;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.Z: OnUndo(this, e); e.Handled = true; break;
                case Key.C: OnCopy(this, e); e.Handled = true; break;
                case Key.S: OnSave(this, e); e.Handled = true; break;
            }
        }
    }

    // Enable the actions that only make sense with an image loaded / a selection.
    private void UpdateState()
    {
        var hasImage = ShotImage.Source is not null;
        EmptyState.Visibility = hasImage ? Visibility.Collapsed : Visibility.Visible;
        CopyButton.IsEnabled = hasImage;
        SaveButton.IsEnabled = hasImage;
        UndoButton.IsEnabled = _undo.Count > 0;
        DeleteButton.IsEnabled = ShotList.SelectedItem is not null;
    }

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    // The custom title-bar close button hides the window to the tray, exactly
    // like the old native X did (handled by OnClosing below).
    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Closing (X) hides to the tray; the app keeps running and is shut down
        // from the tray's Quit command.
        e.Cancel = true;
        Hide();

        base.OnClosing(e);
    }
}
