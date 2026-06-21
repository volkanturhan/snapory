using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Snapory.Services;

// WinForms is enabled for the tray, so disambiguate everything this window uses
// down to its WPF type.
using TextBox = System.Windows.Controls.TextBox;
using Rectangle = System.Windows.Shapes.Rectangle;
using Path = System.Windows.Shapes.Path;
using Color = System.Windows.Media.Color;
using Brush = System.Windows.Media.Brush;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using Point = System.Windows.Point;
using Clipboard = System.Windows.Clipboard;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Key = System.Windows.Input.Key;
using Keyboard = System.Windows.Input.Keyboard;
using ModifierKeys = System.Windows.Input.ModifierKeys;
using Canvas = System.Windows.Controls.Canvas;
using DrawingBitmap = System.Drawing.Bitmap;
using ShotModel = Snapory.Models.Shot;

namespace Snapory;

/// <summary>
/// The annotation editor: shows the captured region and lets the user draw
/// arrows, boxes, highlights, and text over it, then copy the result to the
/// clipboard or save it as a PNG. Annotations live on a transparent canvas above
/// the image; copy/save flatten the two into one bitmap.
/// </summary>
public partial class EditorWindow : Window
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

    private int _pixelWidth;
    private int _pixelHeight;
    private readonly List<UIElement> _undo = new();
    private readonly Services.CaptureHistory _history;

    private Tool _tool = Tool.Arrow;
    private Color _color = Swatches[0];
    private UIElement? _active;
    private Point _start;

    // The history slot this edit occupies once it has been copied or saved, so
    // repeated copy/save updates the same entry instead of piling up duplicates.
    private ShotModel? _savedShot;

    public EditorWindow(BitmapSource image, Services.CaptureHistory history)
    {
        InitializeComponent();

        _history = history;
        BuildSwatches();
        ArrowTool.IsChecked = true;
        PreviewKeyDown += OnPreviewKeyDown;

        LoadImage(image);
    }

    /// <summary>
    /// Loads a fresh capture into this (reused) editor: clears any previous
    /// annotations and sizes the surface to the new image's exact pixels.
    /// </summary>
    public void LoadImage(BitmapSource image)
    {
        Annotations.Children.Clear();
        _undo.Clear();
        _active = null;
        _savedShot = null;

        _pixelWidth = image.PixelWidth;
        _pixelHeight = image.PixelHeight;

        Shot.Source = image;
        Shot.Width = _pixelWidth;
        Shot.Height = _pixelHeight;
        Annotations.Width = _pixelWidth;
        Annotations.Height = _pixelHeight;
    }

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

        // Keep the four tools mutually exclusive.
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
        var pt = e.GetPosition(Annotations);

        if (_tool == Tool.Text)
        {
            AddTextBox(pt);
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

        // Drop an accidental zero-size shape so it does not linger invisibly.
        if (_active is Rectangle { Width: < 3, Height: < 3 } stray)
        {
            Annotations.Children.Remove(stray);
            _undo.Remove(stray);
        }

        _active = null;
        if (Annotations.IsMouseCaptured)
            Annotations.ReleaseMouseCapture();
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
        Fill = Brushes_Transparent,
    };

    private Rectangle NewHighlight() => new()
    {
        Fill = Frozen(Color.FromArgb(0x66, _color.R, _color.G, _color.B)),
    };

    private void AddTextBox(Point at)
    {
        var box = new TextBox
        {
            Background = Brushes_Transparent,
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

        // Let the control appear before focusing it for typing.
        Dispatcher.BeginInvoke(() => box.Focus());
    }

    private static void LayoutRect(Rectangle rect, Point a, Point b)
    {
        var x = Math.Min(a.X, b.X);
        var y = Math.Min(a.Y, b.Y);
        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, y);
        rect.Width = Math.Abs(b.X - a.X);
        rect.Height = Math.Abs(b.Y - a.Y);
    }

    // An arrow as a single stroked geometry: the shaft plus two short head lines.
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

                // The shaft direction rotated by ±angle, stepped back from the tip.
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
    }

    private void OnCopy(object sender, RoutedEventArgs e)
    {
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

    // Record the finished image in the history, reusing this edit's slot if it
    // already has one.
    private void SaveToHistory(BitmapSource image)
    {
        if (_savedShot is null)
            _savedShot = _history.Add(image);
        else
            _history.Update(_savedShot, image);
    }

    // Flatten the image and its annotations into one bitmap at full resolution.
    private BitmapSource RenderFlattened()
    {
        // Drop keyboard focus first so a text caret is not baked into the image.
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
        else if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Closing (X or Esc) just hides the editor to the tray; the app keeps
        // running and is shut down from the tray's Quit command. The same window
        // is reused for the next capture.
        e.Cancel = true;
        Hide();

        base.OnClosing(e);
    }

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static readonly Brush Brushes_Transparent = Frozen(Colors.Transparent);
}
