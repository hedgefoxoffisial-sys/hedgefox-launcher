using System;
using System.Linq;
using Gml.Launcher.ViewModels.HedgeFox;
using ReactiveUI;
using Xunit;

namespace HedgeFox.Launcher.Tests;

/// <summary>
///     The sidebar's moves (<see cref="HfNav.Go" />): the main screen stays in the stack under at most one section
///     page, so the download and the game keep running underneath and the stack never grows.
/// </summary>
public class NavTests
{
    private static readonly Func<IRoutableViewModel, bool> IsHome = p => p.UrlPathSegment == "overview";

    private static Func<IRoutableViewModel, bool> Is(string name) => p => p.UrlPathSegment == name;

    private static (TestScreen Screen, string[] Stack) Run(Action<TestScreen> moves, params string[] start)
    {
        var screen = new TestScreen();
        foreach (var name in start)
            screen.Router.NavigationStack.Add(new Page(screen, name));
        moves(screen);
        return (screen, screen.Router.NavigationStack.Select(p => p.UrlPathSegment!).ToArray());
    }

    private static void Go(TestScreen s, string name) =>
        HfNav.Go(s.Router, IsHome, Is(name), name == "overview" ? null : () => new Page(s, name));

    [Fact]
    public void ASectionGoesOnTopOfTheMainScreen()
    {
        var (_, stack) = Run(s => Go(s, "news"), "overview");
        Assert.Equal(["overview", "news"], stack);
    }

    [Fact]
    public void AnotherSectionTakesThePlaceOfTheOneShown()
    {
        var (_, stack) = Run(s =>
        {
            Go(s, "news");
            Go(s, "settings");
            Go(s, "profile");
        }, "overview");
        Assert.Equal(["overview", "profile"], stack);
    }

    [Fact]
    public void PlayGoesBackToTheMainScreen()
    {
        var (_, stack) = Run(s => Go(s, "overview"), "overview", "settings");
        Assert.Equal(["overview"], stack);
    }

    [Fact]
    public void TheSectionShownIsNotBuiltAgain()
    {
        var built = 0;
        var (_, stack) = Run(s =>
        {
            HfNav.Go(s.Router, IsHome, Is("news"), () => { built++; return new Page(s, "news"); });
            HfNav.Go(s.Router, IsHome, Is("news"), () => { built++; return new Page(s, "news"); });
        }, "overview");
        Assert.Equal(1, built);
        Assert.Equal(["overview", "news"], stack);
    }

    [Fact]
    public void NothingHappensBeforeSignIn()
    {
        var (_, stack) = Run(s => Go(s, "news"), "login");
        Assert.Equal(["login"], stack);
    }

    [Fact]
    public void TheLatestMainScreenCountsAndPagesAboveTheSectionGo()
    {
        var (_, stack) = Run(s => Go(s, "news"), "login", "overview", "settings", "mods");
        Assert.Equal(["login", "overview", "news"], stack);
    }

    [Fact]
    public void ARouterPageThatIsNotASectionHasNoSidebar() =>
        Assert.Equal(HfSection.None, HfNav.SectionOf(new Page(new TestScreen(), "login")));

    private sealed class TestScreen : IScreen
    {
        public RoutingState Router { get; } = new();
    }

    private sealed class Page(IScreen screen, string name) : ReactiveObject, IRoutableViewModel
    {
        public string UrlPathSegment => name;
        public IScreen HostScreen { get; } = screen;
    }
}
