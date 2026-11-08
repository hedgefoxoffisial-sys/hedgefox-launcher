using Gml.Launcher.ViewModels.Pages;
using ReactiveUI;

namespace Gml.Launcher.Views.HedgeFox;

/// <summary>
///     The HedgeFox pages for the stock page view models, hooked into AppViewLocator by one line. The mods are a
///     section of Настройки now; the stock mods page (restyled by the theme only) stays for whatever still opens it.
/// </summary>
public static class HfViews
{
    public static IViewFor? Resolve(object? viewModel) => viewModel switch
    {
        LoginPageViewModel page => new HfLoginView { DataContext = page },
        OverviewPageViewModel page => new HfOverviewView { DataContext = page },
        ProfilePageViewModel page => new HfProfileView { DataContext = page },
        SettingsPageViewModel page => new HfSettingsView { DataContext = page },
        _ => null
    };
}
