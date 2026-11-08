using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Gml.Launcher.Core.HedgeFox;

namespace Gml.Launcher.Views.HedgeFox;

public partial class HfSplashView : UserControl
{
    private readonly Stopwatch _shown = new();
    private readonly TextBlock _hint;
    private DispatcherTimer? _timer;

    public HfSplashView()
    {
        AvaloniaXamlLoader.Load(this);
        // the 1920×1080 scene is scaled down: smooth here; the title and the cogs stay pixel-sharp
        RenderOptions.SetBitmapInterpolationMode(this.Get<Image>("Scene"), BitmapInterpolationMode.HighQuality);
        _hint = this.Get<TextBlock>("Hint");
        _hint.Text = HfStrings.SplashHintAt(TimeSpan.Zero);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _shown.Start();
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(200), DispatcherPriority.Background,
            (_, _) => _hint.Text = HfStrings.SplashHintAt(_shown.Elapsed));
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer?.Stop();
        _timer = null;
        _shown.Stop();
    }

    // the splash has no title bar: it moves by dragging anywhere
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (TopLevel.GetTopLevel(this) is Window w && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            w.BeginMoveDrag(e);
    }
}
