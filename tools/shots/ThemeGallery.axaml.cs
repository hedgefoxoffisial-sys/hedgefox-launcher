using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HedgeFox.Shots;

public partial class ThemeGallery : UserControl
{
    public ThemeGallery()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>Forces the interactive states the screenshot cannot reach with a pointer.</summary>
    public void ForceStates()
    {
        Set("PrimaryHover", ":pointerover");
        Set("PrimaryPressed", ":pressed");
        Set("SecondaryHover", ":pointerover");
        Set("SecondaryPressed", ":pressed");
        Set("GhostHover", ":pointerover");
        Set("LinkHover", ":pointerover");
        Set("InputHover", ":pointerover");
        Set("InputFocus", ":focus");
        Set("PrimaryFocus", ":focus-visible");
        this.FindControl<ScrollViewer>("Scroller")?.ScrollToEnd();
    }

    private void Set(string name, string pseudoClass)
    {
        if (this.FindControl<Control>(name) is { } control)
            ((IPseudoClasses)control.Classes).Set(pseudoClass, true);
    }
}
