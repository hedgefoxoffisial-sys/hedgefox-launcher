using System;
using Avalonia;
using Avalonia.Controls;

namespace Gml.Launcher.Views.HedgeFox.Controls;

/// <summary>
///     Places its children centred at <see cref="Fraction" /> of a slider's travel: the slider's thumb centre moves
///     from <see cref="Inset" /> to width − <see cref="Inset" /> (half the thumb's width). The «советуем» mark on the
///     memory slider sits in one of these, right above the track. A child never leaves the panel's bounds.
/// </summary>
public class HfMarkPanel : Panel
{
    public static readonly StyledProperty<double> FractionProperty =
        AvaloniaProperty.Register<HfMarkPanel, double>(nameof(Fraction));

    public static readonly StyledProperty<double> InsetProperty =
        AvaloniaProperty.Register<HfMarkPanel, double>(nameof(Inset));

    static HfMarkPanel()
    {
        AffectsArrange<HfMarkPanel>(FractionProperty, InsetProperty);
    }

    public double Fraction
    {
        get => GetValue(FractionProperty);
        set => SetValue(FractionProperty, value);
    }

    public double Inset
    {
        get => GetValue(InsetProperty);
        set => SetValue(InsetProperty, value);
    }

    /// <summary>Left edge of a child of <paramref name="childWidth" /> centred on the mark, kept inside the panel.</summary>
    public static double ChildLeft(double width, double inset, double fraction, double childWidth)
    {
        var centre = inset + Math.Clamp(fraction, 0, 1) * Math.Max(0, width - 2 * inset);
        return Math.Clamp(Math.Round(centre - childWidth / 2), 0, Math.Max(0, width - childWidth));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var height = 0.0;
        foreach (var child in Children)
        {
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            height = Math.Max(height, child.DesiredSize.Height);
        }

        return new Size(double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            var w = child.DesiredSize.Width;
            child.Arrange(new Rect(ChildLeft(finalSize.Width, Inset, Fraction, w), 0, w, finalSize.Height));
        }

        return finalSize;
    }
}
