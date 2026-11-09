using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Gml.Launcher.ViewModels.HedgeFox;
using Gml.Launcher.ViewModels.Pages;

namespace Gml.Launcher.Views.HedgeFox;

public partial class HfOverviewView : HfPageView<OverviewPageViewModel, HfOverviewModel>
{
    public HfOverviewView()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override HfOverviewModel CreateModel(OverviewPageViewModel page) => new(page);

    private TopLevel? _top;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _top = TopLevel.GetTopLevel(this);
        _top?.AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _top?.RemoveHandler(KeyDownEvent, OnKey);
        _top = null;
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>Escape closes the open list of versions, wherever the focus is.</summary>
    private void OnKey(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || Model is not { Picker.IsOpen: true } model) return;
        model.Picker.CloseCommand.Execute().Subscribe();
        e.Handled = true;
    }

    /// <summary>A click beside the open list of versions closes it.</summary>
    private void OnDismiss(object? sender, PointerPressedEventArgs e)
    {
        Model?.Picker.CloseCommand.Execute().Subscribe();
        e.Handled = true;
    }
}
