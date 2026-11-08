using System;
using System.IO;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
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
///     The main screen's own state next to the stock <see cref="OverviewPageViewModel" /> (profiles, «Играть», the
///     download and the pages behind it stay upstream's): the version of the game picked for this PC (or the player's
///     pick from Настройки, <see cref="HfProfilePick" />), the skin's face, the download on the sign in Russian and the
///     status line. «Версия игры», «Моды» and «Переустановить» live in Настройки (<see cref="HfSettingsModel" />),
///     «Выйти» in Профиль.
/// </summary>
public sealed class HfOverviewModel : ReactiveObject, IHfScreenModel
{
    private readonly ISkinService? _skins;
    private readonly IHardwareProbe? _hardware;
    private readonly IStorageService? _storage;
    private HfAdvice _advice = new(null, false);
    private string? _choice;
    private bool _choiceLoaded;

    public HfOverviewModel(OverviewPageViewModel page, HfServerInfoModel? info = null)
    {
        Page = page;
        // no news on this screen: the site and Telegram carry them
        Info = info ?? HfServerInfoModel.FromServices(0);
        _skins = Locator.Current.GetService<ISkinService>();
        _hardware = Locator.Current.GetService<IHardwareProbe>();
        _storage = Locator.Current.GetService<IStorageService>();
        Nick = page.User.Name;
    }

    public OverviewPageViewModel Page { get; }
    public HfServerInfoModel Info { get; }
    public string Nick { get; }

    [Reactive] public Bitmap? Face { get; private set; }
    [Reactive] public bool CanPlay { get; private set; }
    [Reactive] public bool IsBusy { get; private set; }

    /// <summary>Nothing downloads or runs: the corners (Профиль, Настройки) and the status line are on.</summary>
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
        var d = new CompositeDisposable(Info.Start());
        var list = Page.ListViewModel;

        // the stock page restores the last selected profile after it loads the list: pick again whenever it does
        d.Add(list.WhenAnyValue(l => l.Profiles, l => l.SelectedProfile)
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
        _ = LoadAdviceAsync();
        _ = LoadFaceAsync();
        return d;
    }

    /// <summary>The player's pick, else the version advised for this PC; never mid-download or mid-game (code-8).</summary>
    private void PickVersion()
    {
        var list = Page.ListViewModel;
        if (!_choiceLoaded || Page.IsProcessing || list.Profiles is not { Count: > 0 } profiles)
            return;
        if (HfProfilePick.Pick(profiles, _advice, _choice) is { } pick && !ReferenceEquals(pick, list.SelectedProfile))
            list.SelectedProfile = pick;
    }

    private async Task LoadChoiceAsync()
    {
        try
        {
            _choice = await HfVersionChoice.GetAsync(_storage);
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

    private async Task LoadAdviceAsync()
    {
        if (_hardware is null)
            return;
        try
        {
            _advice = HardwareAdvisor.Advise(await _hardware.ReadAsync());
            RxApp.MainThreadScheduler.Schedule(PickVersion);
        }
        catch (Exception exception)
        {
            SentrySdk.CaptureException(exception);
        }
    }

    private async Task LoadFaceAsync()
    {
        if (_skins is null)
            return;
        try
        {
            var skin = await _skins.GetAsync(Page.User.Uuid);
            // 8×8 face at ×5: whole pixels, no scaling on screen
            var face = await Task.Run(() => HfSkinImages.Face(skin.Png, 5));
            RxApp.MainThreadScheduler.Schedule(() => Face = new Bitmap(new MemoryStream(face)));
        }
        catch (Exception exception)
        {
            SentrySdk.CaptureException(exception);
        }
    }
}
