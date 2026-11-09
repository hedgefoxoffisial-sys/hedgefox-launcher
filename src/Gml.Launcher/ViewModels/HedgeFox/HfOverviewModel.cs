using System;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Gml.Launcher.Core.HedgeFox;
using Gml.Launcher.Core.Services;
using Gml.Launcher.ViewModels.Pages;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Sentry;
using Splat;

namespace Gml.Launcher.ViewModels.HedgeFox;

/// <summary>
///     «Играть»'s own state next to the stock <see cref="OverviewPageViewModel" /> (profiles, «Играть», the download
///     and the pages behind it stay upstream's): the version of the game picked for this PC or by the player
///     (<see cref="HfProfilePick" />, the bottom bar's <see cref="Picker" />), the three latest posts, the download on
///     the sign in Russian and the status. The game joins the server by itself once it has loaded (the profiles carry
///     --quickPlayMultiplayer). Memory, mods and «Переустановить» live in Настройки (<see cref="HfSettingsModel" />),
///     the player and «Выйти» in the sidebar and Профиль (<see cref="HfShellModel" />).
/// </summary>
public sealed class HfOverviewModel : ReactiveObject, IHfScreenModel
{
    /// <summary>The posts in the row under the title.</summary>
    public const int NewsInRow = 3;

    private readonly IStorageService? _storage;
    private bool _choiceLoaded;

    public HfOverviewModel(OverviewPageViewModel page, HfServerInfoModel? info = null)
    {
        Page = page;
        Info = info ?? HfServerInfoModel.FromServices(NewsInRow);
        _storage = Locator.Current.GetService<IStorageService>();
        Picker = new HfVersionPicker(page, Locator.Current.GetService<IHardwareProbe>(), _storage);
        GoNewsCommand = ReactiveCommand.Create(() =>
        {
            if (page.HostScreen is MainWindowViewModel main)
                HfNav.Show(main, HfSection.News);
        });
    }

    public OverviewPageViewModel Page { get; }
    public HfServerInfoModel Info { get; }
    public HfVersionPicker Picker { get; }

    /// <summary>«Все новости» over the cards: the «Новости» section.</summary>
    public ReactiveCommand<Unit, Unit> GoNewsCommand { get; }

    [Reactive] public bool CanPlay { get; private set; }
    [Reactive] public bool IsBusy { get; private set; }

    /// <summary>Nothing downloads or runs: «ИГРАТЬ» and «Сразу на сервер» are on.</summary>
    [Reactive] public bool IsIdle { get; private set; } = true;
    [Reactive] public double MeterValue { get; private set; }
    [Reactive] public bool MeterIndeterminate { get; private set; }

    /// <summary>On the sign while it downloads: «42 %», or «ЗАГРУЗКА…» while the size is unknown.</summary>
    [Reactive] public string SignLabel { get; private set; } = string.Empty;

    /// <summary>Under the sign while it downloads: «Осталось 312 файлов».</summary>
    [Reactive] public string StepLine { get; private set; } = string.Empty;

    /// <summary>
    ///     «Переустановить» deletes the profile's folder and downloads it again: never mid-download or mid-game, and
    ///     not offline, where the stock loader deletes the files but cannot download them again.
    /// </summary>
    public static bool CanReinstallWith(bool hasProfile, bool busy, bool backendActive) =>
        hasProfile && !busy && backendActive;

    public IDisposable Start()
    {
        var d = new CompositeDisposable(Info.Start(), Picker.Start());
        var list = Page.ListViewModel;

        // the stock page restores the last selected profile after it loads the list: pick again whenever it does
        d.Add(list.WhenAnyValue(l => l.Profiles, l => l.SelectedProfile)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => PickVersion()));
        // the hardware advice arrives later than the list
        d.Add(Picker.WhenAnyValue(p => p.Advice)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => PickVersion()));

        d.Add(Page.WhenAnyValue(p => p.IsProcessing, p => p.LoadingPercentage, p => p.Headline, p => p.Description)
            .CombineLatest(list.WhenAnyValue(l => l.SelectedProfile))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(x =>
            {
                var ((busy, percent, headline, description), selected) = x;
                IsBusy = busy;
                IsIdle = !busy;
                CanPlay = selected is not null && !busy;
                MeterIndeterminate = busy && percent is null;
                MeterValue = percent ?? 0;
                SignLabel = HfPlayProgress.SignLabel(percent);
                StepLine = HfPlayProgress.Step(headline, description);
            }));

        _ = LoadChoiceAsync();
        return d;
    }

    /// <summary>
    ///     The player's pick, else the version advised for this PC; never mid-download or mid-game (code-8). The pick
    ///     is read from memory each time, so a pick in the bottom bar or in Настройки holds.
    /// </summary>
    private void PickVersion()
    {
        var list = Page.ListViewModel;
        if (!_choiceLoaded || Page.IsProcessing || list.Profiles is not { Count: > 0 } profiles)
            return;
        if (HfProfilePick.Pick(profiles, Picker.Advice, HfVersionChoice.Peek()) is { } pick && !ReferenceEquals(pick, list.SelectedProfile))
            list.SelectedProfile = pick;
    }

    private async Task LoadChoiceAsync()
    {
        try
        {
            await HfVersionChoice.GetAsync(_storage);
        }
        catch (Exception exception)
        {
            SentrySdk.CaptureException(exception);
        }
        RxApp.MainThreadScheduler.Schedule(() =>
        {
            _choiceLoaded = true;
            PickVersion();
        });
    }
}
