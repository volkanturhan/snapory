using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace Snapory.Services;

/// <summary>
/// Grabs a single snapshot of the whole desktop (all monitors). Snapory freezes
/// this snapshot the moment the hotkey is pressed and lets the user select a
/// region from it, so what they mark up is exactly what was on screen.
///
/// The bitmap is in physical pixels; combined with a Per-Monitor-DPI-aware
/// process (see app.manifest) the virtual-screen rectangle maps straight to
/// bitmap pixels regardless of display scaling.
/// </summary>
public static class ScreenCapture
{
    /// <summary>
    /// Captures the entire virtual desktop. <paramref name="bounds"/> receives
    /// the virtual-screen rectangle in physical pixels (its Left/Top give the
    /// origin of the captured bitmap).
    /// </summary>
    public static Bitmap CaptureVirtualScreen(out Rectangle bounds)
    {
        bounds = SystemInformation.VirtualScreen;

        var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size,
            CopyPixelOperation.SourceCopy);

        return bitmap;
    }
}
