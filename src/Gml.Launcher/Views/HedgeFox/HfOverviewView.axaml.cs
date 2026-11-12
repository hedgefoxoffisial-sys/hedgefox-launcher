using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Styling;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
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

    /// <summary>Escape closes the columns (keeping the chosen version) or the open list of versions, wherever the focus is.</summary>
    private void OnKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && Model is { Welcome.IsOpen: true } welcome)
        {
            welcome.Welcome.GoCommand.Execute().Subscribe();
            e.Handled = true;
            return;
        }
        if (e.Key != Key.Escape || Model is not { Picker.IsOpen: true } model) return;
        model.Picker.CloseCommand.Execute().Subscribe();
        e.Handled = true;
    }

    /// <summary>
    ///     A column of «Давай настроим лаунчер под тебя» comes up and fades in, 0.15 s after the one on its left; the
    ///     columns are built each time the screen opens, so they come in every time.
    /// </summary>
    private void OnWelcomeColumnLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is not Border { DataContext: HfWelcomeColumn column } border) return;
        new Animation
        {
            Duration = TimeSpan.FromMilliseconds(600),
            Delay = TimeSpan.FromMilliseconds(250 + 150 * column.Index),
            Easing = new CubicEaseOut(),
            FillMode = FillMode.Both,
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(OpacityProperty, 0d), new Setter(TranslateTransform.YProperty, 56d) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(OpacityProperty, 1d), new Setter(TranslateTransform.YProperty, 0d) } }
            }
        }.RunAsync(border);
    }

    /// <summary>A click beside the open list of versions closes it.</summary>
    private void OnDismiss(object? sender, PointerPressedEventArgs e)
    {
        Model?.Picker.CloseCommand.Execute().Subscribe();
        e.Handled = true;
    }
}
