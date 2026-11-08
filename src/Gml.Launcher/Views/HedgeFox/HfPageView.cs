using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using Gml.Launcher.ViewModels.HedgeFox;

namespace Gml.Launcher.Views.HedgeFox;

/// <summary>
///     A HedgeFox page bound to a stock page view model (the router and the commands stay upstream's). The element
///     named "Root" gets the HedgeFox model as its DataContext; the model runs while the page is on screen.
/// </summary>
public abstract class HfPageView<TPage, TModel> : ReactiveUserControl<TPage>
    where TPage : class
    where TModel : class, IHfScreenModel
{
    private IDisposable? _running;
    private TPage? _page;

    public TModel? Model { get; private set; }

    protected abstract TModel CreateModel(TPage page);

    private Control? Root => this.FindControl<Control>("Root");

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is not TPage page || ReferenceEquals(page, _page))
            return;
        _page = page;
        Stop();
        Model = CreateModel(page);
        if (Root is { } root)
            root.DataContext = Model;
        if (VisualRoot is not null)
            _running = Model.Start();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (Model is not null && _running is null)
            _running = Model.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Stop();
    }

    private void Stop()
    {
        _running?.Dispose();
        _running = null;
    }
}
