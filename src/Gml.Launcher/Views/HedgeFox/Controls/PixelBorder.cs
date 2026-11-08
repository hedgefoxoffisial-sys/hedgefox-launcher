using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering;

namespace Gml.Launcher.Views.HedgeFox.Controls;

/// <summary>
///     The site's pixel frame: square or chamfered corners (never rounded), a hard offset shadow without blur,
///     a frame drawn inside the bounds, an accent edge on top or left (cards, tickets) and the Minecraft bevel
///     (a light band on top, a dark band at the bottom; pressed buttons swap them). Everything but the shadow is clipped to the chamfer.
///     The shadow is drawn outside the bounds, so leave room for it (margin or panel spacing).
/// </summary>
public class PixelBorder : Decorator, ICustomHitTest
{
    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<PixelBorder, IBrush?>(nameof(Background));

    public static readonly StyledProperty<IBrush?> BorderBrushProperty =
        AvaloniaProperty.Register<PixelBorder, IBrush?>(nameof(BorderBrush));

    public static readonly StyledProperty<double> BorderThicknessProperty =
        AvaloniaProperty.Register<PixelBorder, double>(nameof(BorderThickness));

    public static readonly StyledProperty<double> NotchProperty =
        AvaloniaProperty.Register<PixelBorder, double>(nameof(Notch));

    public static readonly StyledProperty<IBrush?> ShadowBrushProperty =
        AvaloniaProperty.Register<PixelBorder, IBrush?>(nameof(ShadowBrush));

    public static readonly StyledProperty<double> ShadowOffsetProperty =
        AvaloniaProperty.Register<PixelBorder, double>(nameof(ShadowOffset));

    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<PixelBorder, IBrush?>(nameof(AccentBrush));

    public static readonly StyledProperty<double> AccentTopProperty =
        AvaloniaProperty.Register<PixelBorder, double>(nameof(AccentTop));

    public static readonly StyledProperty<double> AccentLeftProperty =
        AvaloniaProperty.Register<PixelBorder, double>(nameof(AccentLeft));

    public static readonly StyledProperty<IBrush?> BevelTopBrushProperty =
        AvaloniaProperty.Register<PixelBorder, IBrush?>(nameof(BevelTopBrush));

    public static readonly StyledProperty<double> BevelTopProperty =
        AvaloniaProperty.Register<PixelBorder, double>(nameof(BevelTop));

    public static readonly StyledProperty<IBrush?> BevelBottomBrushProperty =
        AvaloniaProperty.Register<PixelBorder, IBrush?>(nameof(BevelBottomBrush));

    public static readonly StyledProperty<double> BevelBottomProperty =
        AvaloniaProperty.Register<PixelBorder, double>(nameof(BevelBottom));

    static PixelBorder()
    {
        AffectsRender<PixelBorder>(BackgroundProperty, BorderBrushProperty, BorderThicknessProperty, NotchProperty,
            ShadowBrushProperty, ShadowOffsetProperty, AccentBrushProperty, AccentTopProperty, AccentLeftProperty,
            BevelTopBrushProperty, BevelTopProperty, BevelBottomBrushProperty, BevelBottomProperty);
        AffectsMeasure<PixelBorder>(BorderThicknessProperty, AccentTopProperty, AccentLeftProperty);
    }

    public IBrush? Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public IBrush? BorderBrush
    {
        get => GetValue(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    /// <summary>Frame width, drawn inside the bounds (the site's inset 0 0 0 Npx line).</summary>
    public double BorderThickness
    {
        get => GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    /// <summary>45° pixel-cut corner size: 4 for buttons, 8 for large ones, 3 for badges, 0 for cards.</summary>
    public double Notch
    {
        get => GetValue(NotchProperty);
        set => SetValue(NotchProperty, value);
    }

    public IBrush? ShadowBrush
    {
        get => GetValue(ShadowBrushProperty);
        set => SetValue(ShadowBrushProperty, value);
    }

    /// <summary>Hard shadow offset right and down, no blur: 4 for cards, 6 for plates, 3 for chips.</summary>
    public double ShadowOffset
    {
        get => GetValue(ShadowOffsetProperty);
        set => SetValue(ShadowOffsetProperty, value);
    }

    /// <summary>Colour of the accent edge (purple card top, orange plate top, orange ticket bar).</summary>
    public IBrush? AccentBrush
    {
        get => GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    public double AccentTop
    {
        get => GetValue(AccentTopProperty);
        set => SetValue(AccentTopProperty, value);
    }

    public double AccentLeft
    {
        get => GetValue(AccentLeftProperty);
        set => SetValue(AccentLeftProperty, value);
    }

    public IBrush? BevelTopBrush
    {
        get => GetValue(BevelTopBrushProperty);
        set => SetValue(BevelTopBrushProperty, value);
    }

    public double BevelTop
    {
        get => GetValue(BevelTopProperty);
        set => SetValue(BevelTopProperty, value);
    }

    public IBrush? BevelBottomBrush
    {
        get => GetValue(BevelBottomBrushProperty);
        set => SetValue(BevelBottomBrushProperty, value);
    }

    public double BevelBottom
    {
        get => GetValue(BevelBottomProperty);
        set => SetValue(BevelBottomProperty, value);
    }

    public bool HitTest(Point point) =>
        (Background != null || BorderBrush != null) && new Rect(Bounds.Size).Contains(point);

    private Thickness Inset()
    {
        var b = BorderThickness;
        return new Thickness(Math.Max(b, AccentLeft), Math.Max(b, AccentTop), b, b) + Padding;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var inset = Inset();
        if (Child is null)
            return new Size(inset.Left + inset.Right, inset.Top + inset.Bottom);
        Child.Measure(availableSize.Deflate(inset));
        return Child.DesiredSize.Inflate(inset);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Child?.Arrange(new Rect(finalSize).Deflate(Inset()));
        return finalSize;
    }

    /// <summary>The 8-point polygon of a chamfered rectangle (a plain rectangle when the notch is 0).</summary>
    public static Geometry Chamfer(Rect r, double notch)
    {
        var n = Math.Max(0, Math.Min(notch, Math.Min(r.Width, r.Height) / 2));
        if (n <= 0)
            return new RectangleGeometry(r);

        var g = new StreamGeometry();
        using var c = g.Open();
        c.BeginFigure(new Point(r.Left + n, r.Top), true);
        c.LineTo(new Point(r.Right - n, r.Top));
        c.LineTo(new Point(r.Right, r.Top + n));
        c.LineTo(new Point(r.Right, r.Bottom - n));
        c.LineTo(new Point(r.Right - n, r.Bottom));
        c.LineTo(new Point(r.Left + n, r.Bottom));
        c.LineTo(new Point(r.Left, r.Bottom - n));
        c.LineTo(new Point(r.Left, r.Top + n));
        c.EndFigure(true);
        return g;
    }

    public override void Render(DrawingContext context)
    {
        var r = new Rect(Bounds.Size);
        if (r.Width <= 0 || r.Height <= 0)
            return;

        var notch = Notch;
        if (ShadowBrush is { } shadow && ShadowOffset > 0)
            context.DrawGeometry(shadow, null, Chamfer(r.Translate(new Vector(ShadowOffset, ShadowOffset)), notch));

        var outer = Chamfer(r, notch);
        using (context.PushGeometryClip(outer))
        {
            if (Background is { } bg)
                context.FillRectangle(bg, r);

            if (BorderBrush is { } border && BorderThickness > 0)
            {
                var t = BorderThickness;
                // the inner edge of a 45° cut moves in by t·tan(22.5°) to keep the diagonal band as thick as the sides
                var inner = Chamfer(r.Deflate(t), Math.Max(0, notch - t * 0.414));
                context.DrawGeometry(border, null, new CombinedGeometry(GeometryCombineMode.Exclude, outer, inner));
            }

            if (AccentBrush is { } accent)
            {
                if (AccentTop > 0)
                    context.FillRectangle(accent, new Rect(0, 0, r.Width, AccentTop));
                if (AccentLeft > 0)
                    context.FillRectangle(accent, new Rect(0, 0, AccentLeft, r.Height));
            }

            if (BevelTopBrush is { } top && BevelTop > 0)
                context.FillRectangle(top, new Rect(0, 0, r.Width, BevelTop));
            if (BevelBottomBrush is { } bottom && BevelBottom > 0)
                context.FillRectangle(bottom, new Rect(0, r.Height - BevelBottom, r.Width, BevelBottom));
        }
    }
}
