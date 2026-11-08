using Gml.Launcher.ViewModels.Pages;
using ReactiveUI;

namespace Gml.Launcher.Views.HedgeFox;

/// <summary>
///     The HedgeFox pages for the stock page view models, hooked into AppViewLocator by one line. Mods keeps the
///     stock page (restyled by the theme only).
/// </summary>
public static class HfViews
{
    public static IViewFor? Resolve(object? viewModel) => viewModel switch
    {
        LoginPageViewModel page => new HfLoginView { DataContext = page },
        OverviewPageViewModel page => new HfOverviewView { DataContext = page },
        _ => null
    };
}
