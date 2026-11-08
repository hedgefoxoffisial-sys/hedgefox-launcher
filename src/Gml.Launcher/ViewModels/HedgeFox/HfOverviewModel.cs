using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
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
///     download and the pages behind it stay upstream's): the profile tabs with the hardware advice, the skin's face,
///     the download on the sign in Russian, the status line, the latest post and «Выйти» that resets the router.
///     «Моды» and «Переустановить» live in Настройки (<see cref="HfSettingsModel" />).
/// </summary>
public sealed class HfOverviewModel : ReactiveObject, IHfScreenModel
{
    private readonly ISkinService? _skins;
    private readonly IHardwareProbe? _hardware;
    private readonly IStorageService? _storage;
    private HfAdvice _advice = new(null, false);

    public HfOverviewModel(OverviewPageViewModel page, HfServerInfoModel? info = null)
    {
        Page = page;
        Info = info ?? HfServerInfoModel.FromServices(1);
        _skins = Locator.Current.GetService<ISkinService>();
        _hardware = Locator.Current.GetService<IHardwareProbe>();
        _storage = Locator.Current.GetService<IStorageService>();
        Nick = page.User.Name;

        // no switching profiles or accounts while a download or the game runs on the old page (code-8)
        var idle = this.WhenAnyValue(x => x.IsIdle);
        SelectCommand = ReactiveCommand.Create<HfProfileCard>(card => Page.ListViewModel.SelectedProfile = card.Profile, idle);
        LogoutCommand = ReactiveCommand.CreateFromTask(LogoutAsync, idle);
    }

    public OverviewPageViewModel Page { get; }
    public HfServerInfoModel Info { get; }
    public string Nick { get; }

    public ObservableCollection<HfProfileCard> Cards { get; } = [];
    public ReactiveCommand<HfProfileCard, Unit> SelectCommand { get; }
    public ReactiveCommand<Unit, Unit> LogoutCommand { get; }

    [Reactive] public Bitmap? Face { get; private set; }
    [Reactive] public bool CanPlay { get; private set; }
    [Reactive] public bool IsBusy { get; private set; }
    [Reactive] public bool IsIdle { get; private set; } = true;
    [Reactive] public double MeterValue { get; private set; }
    [Reactive] public bool MeterIndeterminate { get; private set; }

    /// <summary>On the sign while it downloads: «42 %», or «ЗАГРУЗКА…» while the size is unknown.</summary>
    [Reactive] public string SignLabel { get; private set; } = string.Empty;

    /// <summary>Under the sign while it downloads: «Осталось: 312 файлов · 3,5 МБ/с».</summary>
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

        d.Add(list.WhenAnyValue(l => l.Profiles, l => l.SelectedProfile)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => RebuildCards()));

        d.Add(Page.WhenAnyValue(p => p.IsProcessing, p => p.LoadingPercentage, p => p.Headline, p => p.Description,
                p => p.LoadingSpeed)
            .CombineLatest(list.WhenAnyValue(l => l.SelectedProfile))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(x =>
            {
                var ((busy, percent, headline, description, speed), selected) = x;
                IsBusy = busy;
                IsIdle = !busy;
                CanPlay = selected is not null && !busy;
                MeterIndeterminate = busy && percent is null;
                MeterValue = percent ?? 0;
                SignLabel = HfPlayProgress.SignLabel(percent);
                StepLine = HfPlayProgress.Line(headline, description, speed);
            }));

        _ = LoadAdviceAsync();
        _ = LoadFaceAsync();
        return d;
    }

    private void RebuildCards()
    {
        var list = Page.ListViewModel;
        var cards = HfProfileCards.Build(list.Profiles ?? [], _advice, list.SelectedProfile?.Name);
        Cards.Clear();
        foreach (var card in cards)
            Cards.Add(card);
    }

    private async Task LoadAdviceAsync()
    {
        if (_hardware is null)
            return;
        try
        {
            _advice = HardwareAdvisor.Advise(await _hardware.ReadAsync());
            RxApp.MainThreadScheduler.Schedule(RebuildCards);
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

    private Task LogoutAsync()
    {
        if (_storage is null || Page.HostScreen is not MainWindowViewModel main)
            return Task.CompletedTask;
        return HfSession.LogoutAsync(_storage, main.Router, () => new LoginPageViewModel(main, main.OnClosed));
    }
}
