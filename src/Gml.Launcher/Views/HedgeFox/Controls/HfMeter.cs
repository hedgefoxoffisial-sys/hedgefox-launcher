using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Gml.Launcher.Views.HedgeFox.Controls;

/// <summary>
///     The site's loader meter (Intro.astro .fv__meter): a void trough with a 2 px line ring and a hard shadow,
///     filled with 10 px orange / 2 px light-orange stripes that glow. <see cref="Value" /> is 0…100. When
///     <see cref="IsIndeterminate" /> is on, the full trough fills and the stripes march at 10 frames a second.
/// </summary>
public class HfMeter : Control
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<HfMeter, double>(nameof(Value));

    public static readonly StyledProperty<bool> IsIndeterminateProperty =
        AvaloniaProperty.Register<HfMeter, bool>(nameof(IsIndeterminate));

    public static readonly StyledProperty<IBrush?> TroughBrushProperty =
        AvaloniaProperty.Register<HfMeter, IBrush?>(nameof(TroughBrush), Brushes.Black);

    public static readonly StyledProperty<IBrush?> RingBrushProperty =
        AvaloniaProperty.Register<HfMeter, IBrush?>(nameof(RingBrush));

    public static readonly StyledProperty<IBrush?> StripeBrushProperty =
        AvaloniaProperty.Register<HfMeter, IBrush?>(nameof(StripeBrush), Brushes.Orange);

    public static readonly StyledProperty<IBrush?> StripeLightBrushProperty =
        AvaloniaProperty.Register<HfMeter, IBrush?>(nameof(StripeLightBrush));

    public static readonly StyledProperty<Color> GlowColorProperty =
        AvaloniaProperty.Register<HfMeter, Color>(nameof(GlowColor), Colors.Transparent);

    public static readonly StyledProperty<double> ShadowOffsetProperty =
        AvaloniaProperty.Register<HfMeter, double>(nameof(ShadowOffset), 4);

    private const double Ring = 2, Pad = 3, Stripe = 10, StripeLight = 2;
    private DispatcherTimer? _march;
    private int _phase;

    static HfMeter()
    {
        AffectsRender<HfMeter>(ValueProperty, IsIndeterminateProperty, TroughBrushProperty, RingBrushProperty,
            StripeBrushProperty, StripeLightBrushProperty, GlowColorProperty, ShadowOffsetProperty);
    }

    public HfMeter()
    {
        Height = 18;
        MinWidth = 40;
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

    public IBrush? TroughBrush
    {
        get => GetValue(TroughBrushProperty);
        set => SetValue(TroughBrushProperty, value);
    }

    public IBrush? RingBrush
    {
        get => GetValue(RingBrushProperty);
        set => SetValue(RingBrushProperty, value);
    }

    public IBrush? StripeBrush
    {
        get => GetValue(StripeBrushProperty);
        set => SetValue(StripeBrushProperty, value);
    }

    public IBrush? StripeLightBrush
    {
        get => GetValue(StripeLightBrushProperty);
        set => SetValue(StripeLightBrushProperty, value);
    }

    public Color GlowColor
    {
        get => GetValue(GlowColorProperty);
        set => SetValue(GlowColorProperty, value);
    }

    public double ShadowOffset
    {
        get => GetValue(ShadowOffsetProperty);
        set => SetValue(ShadowOffsetProperty, value);
    }

    /// <summary>The filled share of the trough, 0…1 (full while indeterminate).</summary>
    public static double Fraction(double value, bool indeterminate) =>
        indeterminate ? 1 : double.IsNaN(value) ? 0 : Math.Clamp(value / 100, 0, 1);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsIndeterminateProperty || change.Property == IsVisibleProperty)
            UpdateMarch();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateMarch();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        UpdateMarch();
    }

    private void UpdateMarch()
    {
        var run = IsIndeterminate && IsVisible && this.GetVisualRoot() != null;
        if (run && _march is null)
        {
            _march = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Background, (_, _) =>
            {
                _phase = (_phase + 1) % (int)(Stripe + StripeLight);
                InvalidateVisual();
            });
            _march.Start();
        }
        else if (!run && _march is not null)
        {
            _march.Stop();
            _march = null;
        }
    }

    public override void Render(DrawingContext context)
    {
        var r = new Rect(Bounds.Size);
        if (r.Width < 2 * (Ring + Pad) || r.Height < 2 * (Ring + Pad))
            return;

        if (ShadowOffset > 0 && TroughBrush is { } shadow)
            context.FillRectangle(shadow, r.Translate(new Vector(ShadowOffset, ShadowOffset)));
        if (RingBrush is { } ring)
            context.FillRectangle(ring, r);
        if (TroughBrush is { } trough)
            context.FillRectangle(trough, r.Deflate(Ring));

        var inner = r.Deflate(Ring + Pad);
        var fill = new Rect(inner.X, inner.Y, Math.Round(inner.Width * Fraction(Value, IsIndeterminate)), inner.Height);
        if (fill.Width <= 0)
            return;

        if (GlowColor.A > 0)
            context.DrawRectangle(StripeBrush, null, new RoundedRect(fill),
                new BoxShadows(new BoxShadow { Blur = 10, Color = GlowColor }));

        using (context.PushClip(fill))
        {
            if (StripeBrush is { } stripe)
                context.FillRectangle(stripe, fill);
            if (StripeLightBrush is { } light)
            {
                var period = Stripe + StripeLight;
                for (var x = fill.X + Stripe - period + _phase; x < fill.Right; x += period)
                    context.FillRectangle(light, new Rect(x, fill.Y, StripeLight, fill.Height));
            }
        }
    }
}
