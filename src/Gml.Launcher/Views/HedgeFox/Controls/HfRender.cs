using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Gml.Launcher.Views.HedgeFox.Controls;

/// <summary>
///     Style-settable switches for Avalonia's render options, which have no styled properties of their own:
///     <c>hf:HfRender.PixelArt="True"</c> scales bitmaps with nearest-neighbour (pixel art must never blur),
///     <c>hf:HfRender.CrispText="True"</c> draws text without sub-pixel colour fringes (Tiny5).
/// </summary>
public class HfRender : AvaloniaObject
{
    public static readonly AttachedProperty<bool> PixelArtProperty =
        AvaloniaProperty.RegisterAttached<HfRender, Visual, bool>("PixelArt");

    public static readonly AttachedProperty<bool> CrispTextProperty =
        AvaloniaProperty.RegisterAttached<HfRender, Visual, bool>("CrispText");

    static HfRender()
    {
        PixelArtProperty.Changed.AddClassHandler<Visual>((v, e) =>
            RenderOptions.SetBitmapInterpolationMode(v,
                e.GetNewValue<bool>() ? BitmapInterpolationMode.None : BitmapInterpolationMode.Unspecified));
        CrispTextProperty.Changed.AddClassHandler<Visual>((v, e) =>
            RenderOptions.SetTextRenderingMode(v,
                e.GetNewValue<bool>() ? TextRenderingMode.Antialias : TextRenderingMode.Unspecified));
    }

    public static bool GetPixelArt(Visual v) => v.GetValue(PixelArtProperty);
    public static void SetPixelArt(Visual v, bool value) => v.SetValue(PixelArtProperty, value);
    public static bool GetCrispText(Visual v) => v.GetValue(CrispTextProperty);
    public static void SetCrispText(Visual v, bool value) => v.SetValue(CrispTextProperty, value);
}
