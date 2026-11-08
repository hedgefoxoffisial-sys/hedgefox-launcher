using System;
using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Gml.Launcher.Core;
using Gml.Launcher.Core.HedgeFox;
using Gml.Launcher.ViewModels;

namespace Gml.Launcher.Views.HedgeFox;

public partial class HfSplashView : UserControl
{
    private readonly Stopwatch _shown = new();
    private readonly TextBlock _hint;
    private DispatcherTimer? _timer;
    private SplashScreenViewModel? _vm;

    public HfSplashView()
    {
        AvaloniaXamlLoader.Load(this);
        // the 1920×1080 scene is scaled down: smooth here; the title and the cogs stay pixel-sharp
        RenderOptions.SetBitmapInterpolationMode(this.Get<Image>("Scene"), BitmapInterpolationMode.HighQuality);
        _hint = this.Get<TextBlock>("Hint");
        _hint.Text = HfStrings.SplashHintAt(TimeSpan.Zero);
    }

    // the status line changes at once when the stock checks report a slow or silent sign-in server
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm is not null)
            _vm.PropertyChanged -= OnStatusChanged;
        _vm = DataContext as SplashScreenViewModel;
        if (_vm is not null)
            _vm.PropertyChanged += OnStatusChanged;
    }

    private void OnStatusChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SplashScreenViewModel.StatusText))
            Dispatcher.UIThread.Post(UpdateLine);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _shown.Start();
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(200), DispatcherPriority.Background, (_, _) => UpdateLine());
        _timer.Start();
    }

    /// <summary>Hints, or the stock checks' «slow» and «not answering» status for the sign-in server (code-5).</summary>
    private void UpdateLine()
    {
        if (_vm is { LocalizationService: { } text } vm)
            _hint.Text = HfStrings.SplashLine(vm.StatusText, _shown.Elapsed,
                text.GetString(SystemConstants.BackendCheckingLonger), text.GetString(SystemConstants.BackendOffline));
        else
            _hint.Text = HfStrings.SplashHintAt(_shown.Elapsed);
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
