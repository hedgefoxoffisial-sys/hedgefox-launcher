using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Gml.Launcher.Core.HedgeFox;

namespace Gml.Launcher.Views.HedgeFox.Controls;

/// <summary>
///     A line with marked words (<see cref="HfMarked" />): «Дальность *7 чанков*» draws «7 чанков» in
///     <see cref="MarkBrush" /> and semibold, the rest in the text's own colour.
/// </summary>
public class HfMarkedText : TextBlock
{
    public static readonly StyledProperty<string?> MarkupProperty =
        AvaloniaProperty.Register<HfMarkedText, string?>(nameof(Markup));

    public static readonly StyledProperty<IBrush?> MarkBrushProperty =
        AvaloniaProperty.Register<HfMarkedText, IBrush?>(nameof(MarkBrush));

    protected override Type StyleKeyOverride => typeof(TextBlock);

    public string? Markup
    {
        get => GetValue(MarkupProperty);
        set => SetValue(MarkupProperty, value);
    }

    public IBrush? MarkBrush
    {
        get => GetValue(MarkBrushProperty);
        set => SetValue(MarkBrushProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MarkupProperty || change.Property == MarkBrushProperty)
            Rebuild();
    }

    private void Rebuild()
    {
        var inlines = new InlineCollection();
        foreach (var part in HfMarked.Parse(Markup ?? string.Empty))
        {
            var run = new Run(part.Text);
            if (part.Marked)
            {
                run.FontWeight = FontWeight.SemiBold;
                if (MarkBrush is { } brush)
                    run.Foreground = brush;
            }
            inlines.Add(run);
        }
        Inlines = inlines;
    }
}
