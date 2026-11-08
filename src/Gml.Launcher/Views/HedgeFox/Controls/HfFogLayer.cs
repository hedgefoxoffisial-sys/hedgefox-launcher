using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace Gml.Launcher.Views.HedgeFox.Controls;

/// <summary>
///     One slow strip of the Pelena's fog (the site's tex/fog.png, 128×32, tiled sideways at ×6 = 768×192 with
///     nearest-neighbour, like the site's widest strip), fading upward and drifting left about 7 px a second.
///     It moves one pixel per tick at ~7 ticks a second and skips ticks while the window is hidden (the game is
///     running) or minimised, so it stays cheap on weak PCs.
///     <see cref="Enabled" /> turns every fog layer off (for a future «без анимации» setting).
/// </summary>
public class HfFogLayer : Control
{
    public static readonly StyledProperty<double> ScaleProperty =
        AvaloniaProperty.Register<HfFogLayer, double>(nameof(Scale), 6);

    private const string FogUri = "avares://Gml.Launcher/Assets/HedgeFox/Images/fog.png";
    private static readonly TimeSpan Step = TimeSpan.FromMilliseconds(140);
    private static Bitmap? _fog;

    private DispatcherTimer? _timer;
    private double _offset;

    static HfFogLayer()
    {
        AffectsRender<HfFogLayer>(ScaleProperty);
    }

    public HfFogLayer()
    {
        IsHitTestVisible = false;
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
    }

    public static bool Enabled { get; set; } = true;

    /// <summary>Whole-pixel scale of the 128×32 texture; the strip is drawn along the bottom edge, one tile high.</summary>
    public double Scale
    {
        get => GetValue(ScaleProperty);
        set => SetValue(ScaleProperty, value);
    }

    private static Bitmap Fog => _fog ??= new Bitmap(AssetLoader.Open(new Uri(FogUri)));

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!Enabled)
            return;
        _timer = new DispatcherTimer(Step, DispatcherPriority.Background, (_, _) => Advance());
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer?.Stop();
        _timer = null;
    }

    private void Advance()
    {
        if (TopLevel.GetTopLevel(this) is Window { IsVisible: false } or Window { WindowState: WindowState.Minimized })
            return;
        var tile = Fog.PixelSize.Width * Scale;
        _offset = (_offset + 1) % tile;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (!Enabled || Bounds.Width <= 0 || Bounds.Height <= 0)
            return;
        var size = new Size(Fog.PixelSize.Width * Scale, Fog.PixelSize.Height * Scale);
        var area = new Rect(Bounds.Size);
        var brush = new ImageBrush(Fog)
        {
            TileMode = TileMode.Tile,
            Stretch = Stretch.Fill,
            DestinationRect = new RelativeRect(-_offset, area.Height - size.Height, size.Width, size.Height,
                RelativeUnit.Absolute)
        };
        var fade = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Colors.White, 0), new GradientStop(Colors.White, 0.35), new GradientStop(Colors.Transparent, 1) }
        };
        // one row of tiles along the bottom edge: the texture must not repeat upward
        var strip = new Rect(0, Math.Max(0, area.Height - size.Height), area.Width, Math.Min(area.Height, size.Height));
        using (context.PushOpacityMask(fade, area))
            context.FillRectangle(brush, strip);
    }
}
