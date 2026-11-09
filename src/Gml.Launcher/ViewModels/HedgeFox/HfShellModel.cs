using System;
using System.IO;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Gml.Launcher.Core.HedgeFox;
using Gml.Launcher.Core.Services;
using Gml.Launcher.ViewModels.Pages;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Sentry;
using Splat;

namespace Gml.Launcher.ViewModels.HedgeFox;

/// <summary>
///     The sidebar once signed in (<c>HfSidebar</c> in MainWindow): the section shown now, the player's face and nick
///     at the bottom (→ Профиль) and the moves between «Играть», «Новости», «Настройки» and «Профиль»
///     (<see cref="HfNav" />). Hidden on the sign-in page. The main screen keeps downloading or running the game
///     underneath whatever section is shown.
/// </summary>
public sealed class HfShellModel : ReactiveObject
{
    private readonly MainWindowViewModel _main;
    private readonly ISkinService? _skins;
    private OverviewPageViewModel? _home;

    public HfShellModel(MainWindowViewModel main, ISkinService? skins = null)
    {
        _main = main;
        _skins = skins ?? Locator.Current.GetService<ISkinService>();
        GoPlayCommand = ReactiveCommand.Create(() => Go(HfSection.Play));
        GoNewsCommand = ReactiveCommand.Create(() => Go(HfSection.News));
        GoSettingsCommand = ReactiveCommand.Create(() => Go(HfSection.Settings));
        GoProfileCommand = ReactiveCommand.Create(() => Go(HfSection.Profile));
        OpenLinkCommand = ReactiveCommand.Create<string>(HfSession.OpenLink);
    }

    [Reactive] public HfSection Section { get; private set; }

    /// <summary>Signed in: the sidebar is on screen.</summary>
    [Reactive] public bool IsShown { get; private set; }

    [Reactive] public bool IsPlay { get; private set; }
    [Reactive] public bool IsNews { get; private set; }
    [Reactive] public bool IsSettings { get; private set; }
    [Reactive] public bool IsProfile { get; private set; }

    [Reactive] public string Nick { get; private set; } = string.Empty;
    [Reactive] public Bitmap? Face { get; private set; }

    public ReactiveCommand<Unit, Unit> GoPlayCommand { get; }
    public ReactiveCommand<Unit, Unit> GoNewsCommand { get; }
    public ReactiveCommand<Unit, Unit> GoSettingsCommand { get; }
    public ReactiveCommand<Unit, Unit> GoProfileCommand { get; }
    public ReactiveCommand<string, Unit> OpenLinkCommand { get; }

    /// <summary>Follows the router; dispose the result when the window closes.</summary>
    public IDisposable Start()
    {
        return _main.Router.CurrentViewModel
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(Show);
    }

    /// <summary>Shows <paramref name="section" />; nothing happens before sign-in or when it is already shown.</summary>
    public void Go(HfSection section) => HfNav.Show(_main, section);

    private void Show(IRoutableViewModel? page)
    {
        var section = HfNav.SectionOf(page);
        Section = section;
        IsShown = section != HfSection.None;
        IsPlay = section == HfSection.Play;
        IsNews = section == HfSection.News;
        IsSettings = section == HfSection.Settings;
        IsProfile = section == HfSection.Profile;

        var home = HfNav.Home(_main.Router);
        if (ReferenceEquals(home, _home))
            return;
        _home = home;
        Nick = home?.User.Name ?? string.Empty;
        Face = null;
        if (home is not null)
            _ = LoadFaceAsync(home);
    }

    private async Task LoadFaceAsync(OverviewPageViewModel home)
    {
        if (_skins is null)
            return;
        try
        {
            var skin = await _skins.GetAsync(home.User.Uuid);
            // 8×8 face at ×4: whole pixels, no scaling on screen
            var face = await Task.Run(() => HfSkinImages.Face(skin.Png, 4));
            RxApp.MainThreadScheduler.Schedule(() =>
            {
                if (ReferenceEquals(home, _home))
                    Face = new Bitmap(new MemoryStream(face));
            });
        }
        catch (Exception exception)
        {
            SentrySdk.CaptureException(exception);
        }
    }
}
