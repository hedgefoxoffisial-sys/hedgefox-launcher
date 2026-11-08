using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Gml.Launcher.Views.HedgeFox;

public partial class HfTitleBar : UserControl
{
    public HfTitleBar()
    {
        AvaloniaXamlLoader.Load(this);
        var bar = this.Get<Border>("Bar");
        bar.PointerPressed += OnBarPressed;
        bar.DoubleTapped += OnBarDoubleTapped;
        this.Get<Button>("MinimizeButton").Click += (_, _) =>
        {
            if (Window is { } w)
                w.WindowState = WindowState.Minimized;
        };
        this.Get<Button>("CloseButton").Click += (_, _) => Window?.Close();
        ApplyChrome(HfWindowChrome.Current);
    }

    private Window? Window => TopLevel.GetTopLevel(this) as Window;

    /// <summary>macOS: room for the traffic lights, no own buttons; Windows and Linux: own pixel buttons.</summary>
    public void ApplyChrome(HfChromeKind kind)
    {
        var mac = kind == HfChromeKind.MacOs;
        this.Get<Border>("MacInset").IsVisible = mac;
        this.Get<StackPanel>("CaptionButtons").IsVisible = !mac;
    }

    private void OnBarPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Button || Window is not { } w || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        if (e.ClickCount == 1)
            w.BeginMoveDrag(e);
    }

    private void OnBarDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Button || Window is not { CanResize: true } w || HfWindowChrome.Current == HfChromeKind.MacOs)
            return;
        w.WindowState = w.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }
}
