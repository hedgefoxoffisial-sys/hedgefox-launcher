using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using Gml.Launcher.ViewModels;
using Gml.Launcher.ViewModels.Pages;
using Gml.Launcher.Views.HedgeFox;
using Gml.Launcher.Views.HedgeFox.Controls;
using ReactiveUI;

namespace Gml.Launcher.Views;

public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    /// <summary>Edge width that resizes the frameless window on Linux.</summary>
    private const double ResizeEdge = 6;

    public MainWindow()
    {
        this.WhenActivated(_ => { });
        AvaloniaXamlLoader.Load(this);
        HfWindowChrome.Apply(this);
        Opened += (_, _) => FitIntoWorkArea();

#if DEBUG
        this.AttachDevTools();
#endif
    }

    /// <summary>
    ///     1180×720 is taller than the work area of a 1080p laptop at 150 %: shrink and centre the window so its title
    ///     bar and footer stay on screen (<see cref="HfWindowFit" />).
    /// </summary>
    private void FitIntoWorkArea()
    {
        if ((Screens.ScreenFromWindow(this) ?? Screens.Primary) is not { } screen)
            return;
        var fit = HfWindowFit.Fit(ClientSize, FrameSize ?? ClientSize, new Size(MinWidth, MinHeight), Position,
            screen.WorkingArea, screen.Scaling);
        if (fit is not { } f)
            return;
        MinWidth = f.MinWidth;
        MinHeight = f.MinHeight;
        Width = f.Width;
        Height = f.Height;
        Position = f.Position;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is not MainWindowViewModel viewModel)
            return;
        viewModel.GameLaunched.Subscribe(isActive =>
        {
            if (isActive)
                Hide();
            else
                Show();
        });
        // the sign-in page has no top row; the others have the player and the gear there, which a toast must not hide
        viewModel.Router.CurrentViewModel.Subscribe(page =>
        {
            if (this.FindControl<Control>("Toasts") is { } toasts)
                toasts.Margin = new Thickness(0, page is LoginPageViewModel or null ? 32 : 88, 0, 0);
        });
    }

    protected override void OnClosed(EventArgs e)
    {
        SendCloseEvent();

        base.OnClosed(e);
    }

    private void SendCloseEvent()
    {
        if (DataContext is MainWindowViewModel viewModel)
            viewModel.OnClosed.OnNext(false);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (ResizeEdgeAt(e.GetPosition(this)) is { } edge)
        {
            BeginResizeDrag(edge, e);
            return;
        }

        // empty background (the night scene) drags the window, as in the stock launcher
        if (e.Source is Rectangle or HfMenuScene)
            BeginMoveDrag(e);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (HfWindowChrome.Current != HfChromeKind.Linux)
            return;
        Cursor = ResizeEdgeAt(e.GetPosition(this)) switch
        {
            WindowEdge.West or WindowEdge.East => new Cursor(StandardCursorType.SizeWestEast),
            WindowEdge.North or WindowEdge.South => new Cursor(StandardCursorType.SizeNorthSouth),
            WindowEdge.NorthWest or WindowEdge.SouthEast => new Cursor(StandardCursorType.TopLeftCorner),
            WindowEdge.NorthEast or WindowEdge.SouthWest => new Cursor(StandardCursorType.TopRightCorner),
            _ => Cursor.Default
        };
    }

    /// <summary>On Linux the window has no system frame, so its edges resize it.</summary>
    internal WindowEdge? ResizeEdgeAt(Point p)
    {
        if (HfWindowChrome.Current != HfChromeKind.Linux || WindowState != WindowState.Normal)
            return null;
        var w = Bounds.Width;
        var h = Bounds.Height;
        var left = p.X <= ResizeEdge;
        var right = p.X >= w - ResizeEdge;
        var top = p.Y <= ResizeEdge;
        var bottom = p.Y >= h - ResizeEdge;
        return (left, right, top, bottom) switch
        {
            (true, _, true, _) => WindowEdge.NorthWest,
            (_, true, true, _) => WindowEdge.NorthEast,
            (true, _, _, true) => WindowEdge.SouthWest,
            (_, true, _, true) => WindowEdge.SouthEast,
            (true, _, _, _) => WindowEdge.West,
            (_, true, _, _) => WindowEdge.East,
            (_, _, true, _) => WindowEdge.North,
            (_, _, _, true) => WindowEdge.South,
            _ => null
        };
    }

    private void WindowClosed(object? sender, EventArgs e)
    {
        SendCloseEvent();
    }
}
