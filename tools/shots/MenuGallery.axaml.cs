using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HedgeFox.Shots;

public partial class MenuGallery : UserControl
{
    public MenuGallery()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>Forces the interactive states the screenshot cannot reach with a pointer.</summary>
    public void ForceStates()
    {
        Set("SignHover", ":pointerover");
        Set("SignFocus", ":focus-visible");
        Set("TabHover", ":pointerover");
        Set("TextHover", ":pointerover");
        Set("TextFocus", ":focus-visible");
        Set("GearHover", ":pointerover");
        Set("FieldFocus", ":focus");
    }

    private void Set(string name, string pseudoClass)
    {
        if (this.FindControl<Control>(name) is { } control)
            ((IPseudoClasses)control.Classes).Set(pseudoClass, true);
    }
}
