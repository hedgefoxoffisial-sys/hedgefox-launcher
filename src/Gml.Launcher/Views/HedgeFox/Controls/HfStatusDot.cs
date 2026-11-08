using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Gml.Launcher.Views.HedgeFox.Controls;

public enum HfDotState
{
    /// <summary>No answer from our API: «Нет данных».</summary>
    Unknown,

    /// <summary>Brass square: waiting, loading.</summary>
    Waiting,

    /// <summary>Green square with the glow: the server is up.</summary>
    Online,

    /// <summary>The server does not answer: «Сервер спит».</summary>
    Offline
}

/// <summary>
///     The site's status square (Hero.astro status ticket): 10 px, brass while waiting, green with a soft glow when
///     live. The colours come from the theme (pseudo-classes :online, :waiting, :offline, :unknown).
/// </summary>
public class HfStatusDot : Control
{
    public static readonly StyledProperty<HfDotState> StateProperty =
        AvaloniaProperty.Register<HfStatusDot, HfDotState>(nameof(State));

    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<HfStatusDot, IBrush?>(nameof(Fill), Brushes.Gray);

    public static readonly StyledProperty<Color> GlowColorProperty =
        AvaloniaProperty.Register<HfStatusDot, Color>(nameof(GlowColor), Colors.Transparent);

    static HfStatusDot()
    {
        AffectsRender<HfStatusDot>(FillProperty, GlowColorProperty, StateProperty);
    }

    public HfStatusDot()
    {
        Width = 10;
        Height = 10;
        UpdatePseudoClasses();
    }

    public HfDotState State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public Color GlowColor
    {
        get => GetValue(GlowColorProperty);
        set => SetValue(GlowColorProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StateProperty)
            UpdatePseudoClasses();
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":unknown", State == HfDotState.Unknown);
        PseudoClasses.Set(":waiting", State == HfDotState.Waiting);
        PseudoClasses.Set(":online", State == HfDotState.Online);
        PseudoClasses.Set(":offline", State == HfDotState.Offline);
    }

    public override void Render(DrawingContext context)
    {
        var r = new Rect(Bounds.Size);
        var shadows = GlowColor.A > 0 ? new BoxShadows(new BoxShadow { Blur = 10, Color = GlowColor }) : default;
        context.DrawRectangle(Fill, null, new RoundedRect(r), shadows);
    }
}
