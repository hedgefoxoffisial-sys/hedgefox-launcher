using System;
using Avalonia;

namespace Gml.Launcher.Views.HedgeFox;

/// <summary>A window size (DIPs), its lowered minimum and its top-left corner on the screen (pixels).</summary>
public readonly record struct HfWindowPlacement(double Width, double Height, double MinWidth, double MinHeight, PixelPoint Position);

/// <summary>
///     Keeps the 1180×720 window (spec «Окно») inside the screen's work area. Avalonia centres a window that is taller
///     than the work area at a negative Y and never shrinks it, so on a 1080p laptop at 150 % (672 DIPs high) the title
///     bar started above the screen and the footer sat under the taskbar. The window shrinks to the work area, its
///     minimum drops only where even 1000×620 does not fit (1366×768 at 125 %), and it is centred again.
/// </summary>
public static class HfWindowFit
{
    /// <param name="client">The content size now (DIPs).</param>
    /// <param name="frame">The whole window with any system frame (DIPs); the same as the client when it has none.</param>
    /// <param name="min">The minimum content size (DIPs).</param>
    /// <param name="position">The window's top-left corner (pixels).</param>
    /// <param name="workArea">The screen without taskbars and docks (pixels).</param>
    /// <param name="scaling">Pixels per DIP on that screen.</param>
    /// <returns>Null when the window already fits where it is.</returns>
    public static HfWindowPlacement? Fit(Size client, Size frame, Size min, PixelPoint position, PixelRect workArea,
        double scaling)
    {
        if (scaling <= 0 || workArea.Width <= 0 || workArea.Height <= 0)
            return null;
        var extraW = Math.Max(0, frame.Width - client.Width);
        var extraH = Math.Max(0, frame.Height - client.Height);
        var width = Math.Min(client.Width, Math.Floor(workArea.Width / scaling - extraW));
        var height = Math.Min(client.Height, Math.Floor(workArea.Height / scaling - extraH));

        var inside = position.X >= workArea.X && position.Y >= workArea.Y &&
                     position.X + frame.Width * scaling <= workArea.Right &&
                     position.Y + frame.Height * scaling <= workArea.Bottom;
        if (width >= client.Width && height >= client.Height && inside)
            return null;

        var frameW = (width + extraW) * scaling;
        var frameH = (height + extraH) * scaling;
        var corner = new PixelPoint(workArea.X + (int)Math.Max(0, (workArea.Width - frameW) / 2),
            workArea.Y + (int)Math.Max(0, (workArea.Height - frameH) / 2));
        return new HfWindowPlacement(width, height, Math.Min(min.Width, width), Math.Min(min.Height, height), corner);
    }
}
