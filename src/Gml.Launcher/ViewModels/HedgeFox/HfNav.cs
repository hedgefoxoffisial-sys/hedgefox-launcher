using System;
using System.Linq;
using Gml.Client.Interfaces;
using Gml.Launcher.Core.Services;
using Gml.Launcher.ViewModels.Pages;
using ReactiveUI;
using Splat;

namespace Gml.Launcher.ViewModels.HedgeFox;

/// <summary>The sections of the sidebar once signed in.</summary>
public enum HfSection
{
    /// <summary>Not a sidebar page (sign-in): no sidebar.</summary>
    None,
    Play,
    News,
    Settings,
    Profile
}

/// <summary>
///     The sidebar's moves in the stock router. The main screen (the stock <see cref="OverviewPageViewModel" />, which
///     runs the profiles, the download and the game) stays in the stack; every other section is one page on top of it,
///     so the stack is never deeper than «вход → главный → раздел» and the main screen keeps running underneath.
/// </summary>
public static class HfNav
{
    public static HfSection SectionOf(IRoutableViewModel? page) => page switch
    {
        OverviewPageViewModel => HfSection.Play,
        HfNewsPageViewModel => HfSection.News,
        SettingsPageViewModel => HfSection.Settings,
        ProfilePageViewModel => HfSection.Profile,
        _ => HfSection.None
    };

    /// <summary>
    ///     Shows a section: «home» pops back to the main screen; another section goes on top of it, in place of the
    ///     section shown now (one change of page, no flash of the main screen in between). Nothing happens without the
    ///     main screen in the stack, or when <paramref name="isTarget" /> already holds for the page on top.
    /// </summary>
    /// <param name="isHome">The main screen («Играть»).</param>
    /// <param name="isTarget">The page of the section asked for.</param>
    /// <param name="make">Builds that page; not called for «home».</param>
    public static void Go(RoutingState router, Func<IRoutableViewModel, bool> isHome,
        Func<IRoutableViewModel, bool> isTarget, Func<IRoutableViewModel>? make)
    {
        var stack = router.NavigationStack;
        var home = -1;
        for (var i = stack.Count - 1; i >= 0; i--)
            if (isHome(stack[i]))
            {
                home = i;
                break;
            }
        if (home < 0 || isTarget(stack[^1]))
            return;

        // anything above the one section page (never there in practice) goes first, from the top
        while (stack.Count > home + 2)
            stack.RemoveAt(stack.Count - 1);

        if (make is null)
        {
            if (stack.Count > home + 1)
                stack.RemoveAt(home + 1);
            return;
        }

        if (stack.Count > home + 1)
            stack[home + 1] = make();
        else
            stack.Add(make());
    }

    /// <summary>
    ///     Shows <paramref name="section" /> of the signed-in launcher: the sidebar, «Все новости» under the cards.
    ///     Nothing happens before sign-in, without the app's services, or when the section is already shown.
    /// </summary>
    public static void Show(MainWindowViewModel main, HfSection section)
    {
        var router = main.Router;
        var gml = Locator.Current.GetService<IGmlClientManager>();
        Func<IRoutableViewModel>? make = section switch
        {
            HfSection.News => () => new HfNewsPageViewModel(main),
            HfSection.Settings when gml is not null && Locator.Current.GetService<ISettingsService>() is { } settings =>
                () => new SettingsPageViewModel(main, Locator.Current.GetService<ILocalizationService>(), settings, gml),
            HfSection.Profile when gml is not null && Home(router) is { } home =>
                () => new ProfilePageViewModel(main, home.User, gml),
            _ => null
        };
        if (section != HfSection.Play && make is null)
            return;
        Go(router, page => page is OverviewPageViewModel, page => SectionOf(page) == section, make);
    }

    /// <summary>The main screen in the stack (the latest, after a new sign-in), or null.</summary>
    public static OverviewPageViewModel? Home(RoutingState router) =>
        router.NavigationStack.OfType<OverviewPageViewModel>().LastOrDefault();
}
