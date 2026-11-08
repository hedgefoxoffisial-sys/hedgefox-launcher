using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Gml.Launcher.Core.HedgeFox;
using Gml.Launcher.ViewModels.Pages;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Sentry;
using Splat;

namespace Gml.Launcher.ViewModels.HedgeFox;

/// <summary>
///     The settings page next to the stock <see cref="SettingsPageViewModel" /> (which loads and saves): memory in
///     gigabytes and the «советуем» mark from the hardware advice on the memory slider; «Моды» and «Переустановить»
///     of the profile chosen on the main screen (the stock <see cref="OverviewPageViewModel" /> under this page).
/// </summary>
public sealed class HfSettingsModel : ReactiveObject, IHfScreenModel
{
    private readonly IHardwareProbe? _hardware;
    private int? _recommendedMb;

    public HfSettingsModel(SettingsPageViewModel page, IHardwareProbe? hardware = null)
    {
        Page = page;
        _hardware = hardware ?? Locator.Current.GetService<IHardwareProbe>();
        Game = page.MainViewModel.Router.NavigationStack.OfType<OverviewPageViewModel>().LastOrDefault();
        HasGame = Game is not null;
        ModsCommand = ReactiveCommand.Create(() => Game?.GoModsCommand.Execute(null), this.WhenAnyValue(x => x.CanMods));
        // back to the main screen first: the download shows on its sign
        Reinstall = new HfConfirmModel(() =>
        {
            if (Game is not { } game)
                return;
            Page.GoBackCommand.Execute(null);
            game.ReinstallCommand.Execute(null);
        }, this.WhenAnyValue(x => x.CanReinstall));
    }

    public SettingsPageViewModel Page { get; }

    /// <summary>The main screen under this page, whose chosen profile «Моды» and «Переустановить» act on.</summary>
    public OverviewPageViewModel? Game { get; }

    public bool HasGame { get; }
    public ReactiveCommand<Unit, Unit> ModsCommand { get; }

    /// <summary>«Переустановить» asks first: it deletes the profile's game folder.</summary>
    public HfConfirmModel Reinstall { get; }

    [Reactive] public bool HasMods { get; private set; }
    [Reactive] public bool CanMods { get; private set; }
    [Reactive] public bool CanReinstall { get; private set; }

    [Reactive] public string RamText { get; private set; } = string.Empty;
    [Reactive] public string MinText { get; private set; } = string.Empty;
    [Reactive] public string MaxText { get; private set; } = string.Empty;
    [Reactive] public bool HasMark { get; private set; }
    [Reactive] public double MarkFraction { get; private set; }
    [Reactive] public string MarkText { get; private set; } = string.Empty;
    [Reactive] public bool IsManualRam { get; private set; }
    [Reactive] public bool IsWindowed { get; private set; }

    public IDisposable Start()
    {
        var d = new CompositeDisposable();
        d.Add(Page.WhenAnyValue(p => p.RamValue, p => p.MinRamValue, p => p.MaxRamValue)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => Update()));
        d.Add(Page.WhenAnyValue(p => p.DynamicRamValue, p => p.FullScreen)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(x =>
            {
                IsManualRam = !x.Item1;
                IsWindowed = !x.Item2;
                Update();
            }));
        if (Game is { } game)
            d.Add(game.WhenAnyValue(g => g.IsModsButtonVisible, g => g.IsProcessing)
                .CombineLatest(game.ListViewModel.WhenAnyValue(l => l.SelectedProfile))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(x =>
                {
                    var ((mods, busy), selected) = x;
                    HasMods = mods;
                    CanMods = mods && !busy && game.BackendIsActive;
                    CanReinstall = HfOverviewModel.CanReinstallWith(selected is not null, busy, game.BackendIsActive);
                }));
        _ = LoadAdviceAsync();
        return d;
    }

    private void Update()
    {
        RamText = IsManualRam ? ProfileCatalog.RamLabel((int)Page.RamValue) : HfStrings.MemoryAutoShort;
        MinText = ProfileCatalog.RamLabel((int)Page.MinRamValue);
        MaxText = ProfileCatalog.RamLabel((int)Page.MaxRamValue);
        var rec = _recommendedMb;
        HasMark = rec is not null && Page.MaxRamValue > Page.MinRamValue;
        MarkFraction = rec is { } mb ? HfRamMark.Fraction(mb, Page.MinRamValue, Page.MaxRamValue) : 0;
        MarkText = rec is { } r ? $"{HfStrings.MemoryRecommendedMark} {ProfileCatalog.RamLabel(r)}" : string.Empty;
    }

    private async Task LoadAdviceAsync()
    {
        if (_hardware is null)
            return;
        try
        {
            var hardware = await _hardware.ReadAsync();
            var advice = HardwareAdvisor.Advise(hardware);
            // the slider's end is what the launcher can give; the advice must fit under it
            var max = Math.Max(Page.MaxRamValue, (ulong)Math.Max(0, hardware.TotalRamMb));
            _recommendedMb = HfRamMark.RecommendedMb(advice, max);
            RxApp.MainThreadScheduler.Schedule(Update);
        }
        catch (Exception exception)
        {
            SentrySdk.CaptureException(exception);
        }
    }
}
