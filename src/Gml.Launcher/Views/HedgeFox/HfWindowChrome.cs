using System;
using Avalonia.Controls;
using Avalonia.Platform;

namespace Gml.Launcher.Views.HedgeFox;

public enum HfChromeKind
{
    Windows,
    MacOs,
    Linux
}

/// <summary>
///     The window frame per OS (spec «Окно»): our own 32 px title bar everywhere; on Windows and Linux our pixel
///     «свернуть» and «закрыть», on macOS the native traffic lights at the left. Opaque, no Acrylic.
/// </summary>
public static class HfWindowChrome
{
    public const double TitleBarHeight = 32;

    /// <summary>Room the macOS traffic lights take at the left of the title bar.</summary>
    public const double MacTrafficLightsInset = 78;

    /// <summary>Lets the screenshot harness draw another OS's frame. Null: the running OS.</summary>
    public static HfChromeKind? Override { get; set; }

    public static HfChromeKind Current =>
        Override ?? (OperatingSystem.IsMacOS() ? HfChromeKind.MacOs
            : OperatingSystem.IsWindows() ? HfChromeKind.Windows
            : HfChromeKind.Linux);

    public static void Apply(Window window)
    {
        window.ExtendClientAreaTitleBarHeightHint = TitleBarHeight;
        switch (Current)
        {
            case HfChromeKind.MacOs:
                window.ExtendClientAreaToDecorationsHint = true;
                window.ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.PreferSystemChrome;
                break;
            case HfChromeKind.Windows:
                // keeps the native resize border and snapping, hides the system caption buttons
                window.ExtendClientAreaToDecorationsHint = true;
                window.ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome;
                break;
            default:
                // X11 cannot extend the client area: no system frame at all, resizing from the edges (MainWindow)
                window.ExtendClientAreaToDecorationsHint = false;
                window.SystemDecorations = SystemDecorations.None;
                break;
        }
    }
}
