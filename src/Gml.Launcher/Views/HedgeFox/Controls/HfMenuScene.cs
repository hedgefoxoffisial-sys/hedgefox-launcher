using System;
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Rendering;
using Avalonia.Threading;

namespace Gml.Launcher.Views.HedgeFox.Controls;

/// <summary>The game menu's sprites (Assets/HedgeFox/Images/Menu, drawn 1:1 by the main repo's gen_branding.py).</summary>
public static class HfMenuArt
{
    private static readonly Dictionary<string, Bitmap> Cache = new();

    public static Bitmap Get(string name)
    {
        if (!Cache.TryGetValue(name, out var bitmap))
            Cache[name] = bitmap = new Bitmap(AssetLoader.Open(new Uri($"avares://Gml.Launcher/Assets/HedgeFox/Images/Menu/{name}")));
        return bitmap;
    }
}

/// <summary>
///     The night of the game's main menu behind every page: the sky from #100A22 to #2A1D4F, pixel stars (a few of
///     them twinkle), the moon on the left, the Pelena peaks with the hub along the bottom, two strips of fog that
///     drift left, and the airship in the top right corner that bobs by 2 px. Pixel art at whole scales with
///     nearest-neighbour; only the airship (a minified illustration) is smooth. One slow timer, paused while the
///     window is hidden (the game runs) or minimised. <see cref="Motion" /> off draws the first frame, no timer.
///     Empty sky is the window's drag area (MainWindow), so the whole control takes the pointer.
/// </summary>
public class HfMenuScene : Control, ICustomHitTest
{
    private static readonly Color SkyTop = Color.FromRgb(0x10, 0x0A, 0x22);
    private static readonly Color SkyLow = Color.FromRgb(0x2A, 0x1D, 0x4F);
    private static readonly Color Text = Color.FromRgb(0xF4, 0xEF, 0xFB);
    private static readonly Color Orange = Color.FromRgb(0xFF, 0x9D, 0x45);
    private static readonly Color Cream = Color.FromRgb(0xFF, 0xE9, 0xA8);
    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(190);

    /// <summary>Fog drift, px a second at ×2 (the game: one 480 px period in 160 s and in 96 s).</summary>
    private const double FogBackSpeed = 3, FogFrontSpeed = 5;

    private const double TwinkleFrameMs = 380, BobHalfMs = 1200;

    // (x, y) as fractions of the width and of the upper sky, size in px, colour
    private static readonly List<(double X, double Y, int Size, Color Colour, byte Alpha)> Stars = MakeStars(90, 8112026);
    private static readonly List<(double X, double Y, int Phase, Color Colour)> Twinkles = MakeTwinkles(24, 2611);

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private DispatcherTimer? _timer;
    private (long FogBack, long FogFront, long Twinkle, long Bob) _frame = (-1, -1, -1, -1);

    public HfMenuScene()
    {
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
    }

    public bool HitTest(Point point) => true;

    /// <summary>Off: the first frame only, no timer (the screenshot harness; a future «без анимации» setting).</summary>
    public static bool Motion { get; set; } = true;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!Motion)
            return;
        _timer = new DispatcherTimer(Tick, DispatcherPriority.Background, (_, _) => Advance());
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer?.Stop();
        _timer = null;
    }

    private double Ms => Motion ? _clock.Elapsed.TotalMilliseconds : 0;

    private void Advance()
    {
        if (TopLevel.GetTopLevel(this) is Window { IsVisible: false } or Window { WindowState: WindowState.Minimized })
            return;
        var ms = Ms;
        // whole texels (2 px) of drift, one twinkle frame, the airship up or down
        var frame = ((long)(ms / 1000 * FogBackSpeed / 2), (long)(ms / 1000 * FogFrontSpeed / 2),
            (long)(ms / TwinkleFrameMs), (long)(ms / BobHalfMs));
        if (frame == _frame)
            return;
        _frame = frame;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 0 || h <= 0)
            return;
        var ms = Ms;

        // sky
        context.FillRectangle(new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(SkyTop, 0), new GradientStop(SkyLow, 1) }
        }, new Rect(0, 0, w, h));

        // stars in the upper 55 %, then the twinkling ones (dim, bright, a plus at the peak, dark again)
        var sky = h * 0.55;
        foreach (var (x, y, size, colour, alpha) in Stars)
            context.FillRectangle(new SolidColorBrush(colour, alpha / 255.0), Snap(x * w, y * sky, size, size));
        var twinkle = (int)(ms / TwinkleFrameMs);
        foreach (var (x, y, phase, colour) in Twinkles)
        {
            var level = new[] { 0, 1, 2, 3, 2, 1, 0, 0 }[((twinkle - phase) % 8 + 8) % 8];
            if (level == 0)
                continue;
            var px = Math.Round(x * w / 2) * 2;
            var py = Math.Round(y * sky / 2) * 2;
            context.FillRectangle(new SolidColorBrush(colour, new[] { 0, 90, 170, 255 }[level] / 255.0), new Rect(px, py, 2, 2));
            if (level == 3)
            {
                var arm = new SolidColorBrush(colour, 130 / 255.0);
                context.FillRectangle(arm, new Rect(px - 2, py, 2, 2));
                context.FillRectangle(arm, new Rect(px + 2, py, 2, 2));
                context.FillRectangle(arm, new Rect(px, py - 2, 2, 2));
                context.FillRectangle(arm, new Rect(px, py + 2, 2, 2));
            }
        }

        // the moon, ×2 (cells of 4 px), left of the title; on a short window it climbs towards the top (17 % of the
        // height at 720 px, 8 px at the minimum 620) so the panels of Настройки and Профиль leave it clear
        var moon = HfMenuArt.Get("moon.png");
        var moonY = h >= 720 ? h * 0.17 : Math.Max(8, h * 0.17 - (720 - h) * 0.97);
        context.DrawImage(moon, new Rect(Math.Round(Math.Max(16, w * 0.035)), Math.Round(moonY), 144, 144));

        // the peaks at a whole scale that covers the width, the hub on the right; never higher than 38 % of the
        // window, so on short windows the snow stays under the menu column
        var peaks = HfMenuArt.Get("peaks.png");
        var s = Math.Max(4, Math.Ceiling(w / peaks.PixelSize.Width));
        var pw = peaks.PixelSize.Width * s;
        var ph = peaks.PixelSize.Height * s;
        var top = Math.Round(Math.Max(h - ph, h * 0.38));
        context.DrawImage(peaks, new Rect(w - pw, top, pw, ph));

        // centre shade (gen_branding.centre_shade): calms the sky behind the centred menu
        context.FillRectangle(new RadialGradientBrush
        {
            Center = new RelativePoint(0.5, 0.46, RelativeUnit.Relative),
            GradientOrigin = new RelativePoint(0.5, 0.46, RelativeUnit.Relative),
            RadiusX = new RelativeScalar(0.42, RelativeUnit.Relative),
            RadiusY = new RelativeScalar(0.62, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(150, SkyTop.R, SkyTop.G, SkyTop.B), 0),
                new GradientStop(Color.FromArgb(95, SkyTop.R, SkyTop.G, SkyTop.B), 0.5),
                new GradientStop(Color.FromArgb(0, SkyTop.R, SkyTop.G, SkyTop.B), 1)
            }
        }, new Rect(0, 0, w, h));

        // the far fog over the land
        DrawFog(context, HfMenuArt.Get("fog-back.png"), w, h - 88, 0.9, (long)(ms / 1000 * FogBackSpeed / 2) * 2);

        // the airship in the top right corner, above the hub and beside the title (the site's first screen): never
        // over the title or the hub; bobbing 2 px (smooth: a minified illustration, not a 1:1 sprite). Like the moon it
        // climbs on a short window (13 % of the height at 720 px, just under the title bar at 620) so it clears the
        // top of the Настройки and Профиль panels
        var ship = HfMenuArt.Get("ship.png");
        var sw = Math.Round(Math.Clamp(w / 2 - 288 - 40, 140, 260));
        var sh = Math.Round(sw * ship.PixelSize.Height / ship.PixelSize.Width);
        var bob = (long)(ms / BobHalfMs) % 2 == 1 ? -2 : 0;
        var shipY = h >= 720 ? h * 0.13 : Math.Max(36, h * 0.13 - (720 - h) * 0.45);
        using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.HighQuality }))
            context.DrawImage(ship, new Rect(Math.Round(w - sw - 12), Math.Round(shipY) + bob, sw, sh));

        // the near fog on the ground
        DrawFog(context, HfMenuArt.Get("fog-front.png"), w, h - 44, 0.85, (long)(ms / 1000 * FogFrontSpeed / 2) * 2);

        // a dark band under the footer links
        context.FillRectangle(new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0, SkyTop.R, SkyTop.G, SkyTop.B), 0),
                new GradientStop(Color.FromArgb(215, SkyTop.R, SkyTop.G, SkyTop.B), 0.7),
                new GradientStop(Color.FromArgb(235, SkyTop.R, SkyTop.G, SkyTop.B), 1)
            }
        }, new Rect(0, h - 96, w, 96));
    }

    /// <summary>One strip of fog at ×2, tiled sideways, drifted left by <paramref name="offset" /> px.</summary>
    private static void DrawFog(DrawingContext context, Bitmap fog, double w, double top, double opacity, long offset)
    {
        var tw = fog.PixelSize.Width * 2.0;
        var th = fog.PixelSize.Height * 2.0;
        var brush = new ImageBrush(fog)
        {
            TileMode = TileMode.Tile,
            Stretch = Stretch.Fill,
            Opacity = opacity,
            DestinationRect = new RelativeRect(-(offset % tw), top, tw, th, RelativeUnit.Absolute)
        };
        context.FillRectangle(brush, new Rect(0, top, w, th));
    }

    private static Rect Snap(double x, double y, double w, double h) =>
        new(Math.Round(x / 2) * 2, Math.Round(y / 2) * 2, w, h);

    // the game's LCG (gen_branding.stars and lcg): the same sky on every start
    private static List<(double, double, int, Color, byte)> MakeStars(int count, long seed)
    {
        var list = new List<(double, double, int, Color, byte)>();
        var state = seed;
        long Next() => state = (state * 1103515245 + 12345) & 0x7FFFFFFF;
        for (var i = 0; i < count; i++)
        {
            var x = Next() % 10000 / 10000.0;
            var y = Next() % 10000 / 10000.0;
            var r = Next();
            list.Add((x, y, r % 7 == 0 ? 4 : 2, r % 3 != 0 ? Text : Orange, (byte)(r % 3 != 0 ? 200 : 170)));
        }
        return list;
    }

    private static List<(double, double, int, Color)> MakeTwinkles(int count, long seed)
    {
        var list = new List<(double, double, int, Color)>();
        var state = seed;
        long Next() => (state = (state * 1103515245 + 12345) & 0x7FFFFFFF) >> 16;
        for (var i = 0; i < count; i++)
        {
            var x = Next() % 1000 / 1000.0;
            var y = Next() % 1000 / 1000.0;
            var warm = Next() % 4 == 0;
            list.Add((x, y, (int)(Next() % 8), warm ? Cream : Text));
        }
        return list;
    }
}

/// <summary>
///     A pixel sprite with frames stacked top to bottom (e.g. the fog tuft under the sign, cloud.png: 6 frames of
///     272×24), drawn at a whole <see cref="Scale" /> with nearest-neighbour, one frame every <see cref="FrameMs" />.
///     Follows <see cref="HfMenuScene.Motion" />.
/// </summary>
public class HfSprite : Control
{
    public static readonly StyledProperty<string?> SourceProperty =
        AvaloniaProperty.Register<HfSprite, string?>(nameof(Source));

    public static readonly StyledProperty<int> FramesProperty =
        AvaloniaProperty.Register<HfSprite, int>(nameof(Frames), 1);

    public static readonly StyledProperty<int> ScaleProperty =
        AvaloniaProperty.Register<HfSprite, int>(nameof(Scale), 2);

    public static readonly StyledProperty<double> FrameMsProperty =
        AvaloniaProperty.Register<HfSprite, double>(nameof(FrameMs), 450);

    private DispatcherTimer? _timer;
    private int _frame;

    static HfSprite()
    {
        AffectsRender<HfSprite>(SourceProperty, FramesProperty, ScaleProperty);
        AffectsMeasure<HfSprite>(SourceProperty, FramesProperty, ScaleProperty);
    }

    public HfSprite()
    {
        IsHitTestVisible = false;
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
    }

    /// <summary>File name in Assets/HedgeFox/Images/Menu.</summary>
    public string? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public int Frames
    {
        get => GetValue(FramesProperty);
        set => SetValue(FramesProperty, value);
    }

    public int Scale
    {
        get => GetValue(ScaleProperty);
        set => SetValue(ScaleProperty, value);
    }

    public double FrameMs
    {
        get => GetValue(FrameMsProperty);
        set => SetValue(FrameMsProperty, value);
    }

    private Bitmap? Bitmap => Source is { Length: > 0 } s ? HfMenuArt.Get(s) : null;

    protected override Size MeasureOverride(Size availableSize) =>
        Bitmap is { } b
            ? new Size(b.PixelSize.Width * Scale, b.PixelSize.Height / Math.Max(1, Frames) * Scale)
            : default;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!HfMenuScene.Motion || Frames <= 1)
            return;
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(FrameMs), DispatcherPriority.Background, (_, _) =>
        {
            if (TopLevel.GetTopLevel(this) is Window { IsVisible: false } or Window { WindowState: WindowState.Minimized })
                return;
            _frame = (_frame + 1) % Math.Max(1, Frames);
            InvalidateVisual();
        });
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer?.Stop();
        _timer = null;
    }

    public override void Render(DrawingContext context)
    {
        if (Bitmap is not { } b)
            return;
        var fh = b.PixelSize.Height / Math.Max(1, Frames);
        var source = new Rect(0, _frame * fh, b.PixelSize.Width, fh);
        context.DrawImage(b, source, new Rect(0, 0, b.PixelSize.Width * Scale, fh * Scale));
    }
}

/// <summary>
///     The wooden sign while the game downloads: the plank (sign.png at ×2) lights up from the left as the light
///     plank (sign-hover.png) is revealed up to <see cref="Value" /> per cent. Indeterminate: the plain plank.
///     <see cref="IsNarrow" />: the 160×32 plank (320×64), as the sign of the bottom bar.
/// </summary>
public class HfSignMeter : Control
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<HfSignMeter, double>(nameof(Value));

    public static readonly StyledProperty<bool> IsIndeterminateProperty =
        AvaloniaProperty.Register<HfSignMeter, bool>(nameof(IsIndeterminate));

    public static readonly StyledProperty<bool> IsNarrowProperty =
        AvaloniaProperty.Register<HfSignMeter, bool>(nameof(IsNarrow));

    /// <summary>The plank inside the sprite's 2-texel glow margin, at ×2.</summary>
    private const double Glow = 4;

    static HfSignMeter()
    {
        AffectsRender<HfSignMeter>(ValueProperty, IsIndeterminateProperty, IsNarrowProperty);
        AffectsMeasure<HfSignMeter>(IsNarrowProperty);
    }

    public HfSignMeter()
    {
        IsHitTestVisible = false;
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public bool IsIndeterminate
    {
        get => GetValue(IsIndeterminateProperty);
        set => SetValue(IsIndeterminateProperty, value);
    }

    public bool IsNarrow
    {
        get => GetValue(IsNarrowProperty);
        set => SetValue(IsNarrowProperty, value);
    }

    private double PlankWidth => IsNarrow ? 320 : 448;

    protected override Size MeasureOverride(Size availableSize) => new(PlankWidth, 64);

    public override void Render(DrawingContext context)
    {
        var plank = HfMenuArt.Get(IsNarrow ? "sign-narrow.png" : "sign.png");
        var lit = HfMenuArt.Get(IsNarrow ? "sign-narrow-hover.png" : "sign-hover.png");
        var w = PlankWidth;
        var rect = new Rect(0, 0, w, 64);
        context.DrawImage(plank, rect);
        if (IsIndeterminate)
            return;
        // whole texels: the lit wood advances 2 px at a time; the wood still to go is dimmed (on the plank only)
        var width = Math.Round((w - 2 * Glow) * Math.Clamp(Value, 0, 100) / 100 / 2) * 2;
        using (context.PushOpacityMask(new ImageBrush(plank) { Stretch = Stretch.Fill }, rect))
            context.FillRectangle(new SolidColorBrush(Color.FromArgb(110, 0x08, 0x05, 0x11)),
                new Rect(Glow + width, 0, w - Glow - (Glow + width), 64));
        if (width <= 0)
            return;
        using (context.PushClip(new Rect(Glow, 0, width, 64)))
            context.DrawImage(lit, rect);
    }
}
