using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Gml.Client.Interfaces;
using Gml.Dto.Mods;
using Gml.Dto.Profile;
using Gml.Launcher.Core.HedgeFox;
using Gml.Launcher.Core.Services;
using Gml.Launcher.ViewModels.Pages;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Sentry;
using Splat;

namespace Gml.Launcher.ViewModels.HedgeFox;

/// <param name="Mod">The stock row the toggle changes (its IsSelected) and passes to the stock command.</param>
/// <param name="Title">The mod's name from GML, «Мини-карта».</param>
/// <param name="Description">One line about it; empty when GML has none.</param>
public sealed record HfModRow(ExternalModReadDto Mod, string Title, string Description)
{
    public bool HasDescription => Description.Length > 0;

    /// <summary>The description on hover; no empty tooltip.</summary>
    public string? Tip => HasDescription ? Description : null;

    /// <summary>The rows in GML's order, with the titles and descriptions GML keeps under «name.jar».</summary>
    public static IReadOnlyList<HfModRow> Build(IEnumerable<ExternalModReadDto> mods,
        IReadOnlyDictionary<string, ModsDetailsInfoDto> details) =>
        mods.Select(m => details.TryGetValue($"{m.Name}.jar", out var info)
                ? new HfModRow(m, string.IsNullOrWhiteSpace(info.Title) ? m.Name : info.Title.Trim(), info.Description?.Trim() ?? string.Empty)
                : new HfModRow(m, m.Name, string.Empty))
            .ToList();
}

/// <summary>
///     The settings page next to the stock <see cref="SettingsPageViewModel" /> (which loads and saves). For the
///     version chosen on the main screen (the stock <see cref="OverviewPageViewModel" /> under this page): «Версия
///     игры» (remembered as the player's pick, <see cref="HfProfilePick" />), memory with one «рекомендуем» mark (the
///     thumb on it is the stock automatic memory), its optional mods (the stock <see cref="ModsPageViewModel" />
///     loads and toggles them) and «Переустановить». Window size and the game folder wait under «Дополнительно».
/// </summary>
public sealed class HfSettingsModel : ReactiveObject, IHfScreenModel
{
    private readonly IGmlClientManager? _gml;
    private readonly ISystemService? _system;
    private readonly SerialDisposable _modsWatch = new();
    private ProfileReadDto? _modsFor;
    private int? _markMb;
    private double _ram;
    private bool _loaded;
    private bool _applying;

    public HfSettingsModel(SettingsPageViewModel page, IHardwareProbe? hardware = null)
    {
        Page = page;
        _gml = Locator.Current.GetService<IGmlClientManager>();
        _system = Locator.Current.GetService<ISystemService>();
        Game = page.MainViewModel.Router.NavigationStack.OfType<OverviewPageViewModel>().LastOrDefault();
        HasGame = Game is not null;
        Picker = Game is { } g
            ? new HfVersionPicker(g, hardware ?? Locator.Current.GetService<IHardwareProbe>(),
                Locator.Current.GetService<IStorageService>())
            : null;
        ToggleModCommand = ReactiveCommand.Create<HfModRow>(row => Mods?.ChangeOptionalModState.Execute(row.Mod),
            this.WhenAnyValue(x => x.CanMods));
        ToggleMoreCommand = ReactiveCommand.Create(() => { ShowMore = !ShowMore; });
        // «Сравнить версии»: back to «Играть», where the three columns of the first login open again
        CompareCommand = ReactiveCommand.Create(() =>
        {
            HfWelcomeRequest.Ask();
            HfNav.Show(page.MainViewModel, HfSection.Play);
        });
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

    /// <summary>The main screen under this page: its version is the one these settings are about.</summary>
    public OverviewPageViewModel? Game { get; }

    public bool HasGame { get; }

    // ----- version of the game -----

    /// <summary>«Версия игры» of the main screen (the same as in its bottom bar); null without the main screen.</summary>
    public HfVersionPicker? Picker { get; }

    // ----- memory -----

    /// <summary>
    ///     The slider: the version's memory while memory is automatic, else the saved value. Moving it saves the value;
    ///     on the «рекомендуем» mark memory is automatic again (it follows the version).
    /// </summary>
    public double Ram
    {
        get => _ram;
        set
        {
            // the slider coerces its value while its ends load; only the player's moves count
            if (!_loaded || value <= 0 || Math.Abs(value - _ram) < 0.5)
                return;
            _ram = value;
            // both stock values change first, so the slider never sees the half-way state mid-drag
            _applying = true;
            try
            {
                Page.RamValue = value;
                Page.DynamicRamValue = HfRamMark.IsOnMark(value, MarkOnSlider);
            }
            finally
            {
                _applying = false;
            }
            this.RaisePropertyChanged();
            ShowRam();
        }
    }

    [Reactive] public string RamText { get; private set; } = string.Empty;
    [Reactive] public bool HasMark { get; private set; }
    [Reactive] public double MarkFraction { get; private set; }
    [Reactive] public bool IsWindowed { get; private set; }

    // ----- mods of the chosen version -----

    /// <summary>The stock mods page model for the chosen version (it loads the list and toggles the files), or null.</summary>
    [Reactive] public ModsPageViewModel? Mods { get; private set; }
    public ObservableCollection<HfModRow> ModRows { get; } = [];
    [Reactive] public bool HasMods { get; private set; }
    [Reactive] public bool CanMods { get; private set; }
    public ReactiveCommand<HfModRow, Unit> ToggleModCommand { get; }

    // ----- the rest -----

    [Reactive] public bool ShowMore { get; private set; }
    public ReactiveCommand<Unit, Unit> ToggleMoreCommand { get; }

    /// <summary>«Сравнить версии» under the versions: the three columns with pictures on «Играть».</summary>
    public ReactiveCommand<Unit, Unit> CompareCommand { get; }

    /// <summary>«Переустановить» asks first: it deletes the version's game folder.</summary>
    public HfConfirmModel Reinstall { get; }

    [Reactive] public bool CanReinstall { get; private set; }

    /// <summary>The mark where the slider can show it: at its end when the version asks for more than this PC has.</summary>
    private int? MarkOnSlider => _markMb is { } mb ? (int)Math.Min(mb, (double)Page.MaxRamValue) : null;

    public IDisposable Start()
    {
        var d = new CompositeDisposable(_modsWatch);
        // the stock page sets the saved values, then the folder; the slider counts from then on
        d.Add(Page.WhenAnyValue(p => p.RamValue, p => p.DynamicRamValue, p => p.MinRamValue, p => p.MaxRamValue,
                p => p.InstallationFolder)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ =>
            {
                _loaded = Page.InstallationFolder is not null && Page.MaxRamValue > 0;
                ShowRam();
            }));
        d.Add(Page.WhenAnyValue(p => p.FullScreen)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(full => IsWindowed = !full));
        if (Game is { } game)
        {
            d.Add(Picker!.Start());
            d.Add(game.ListViewModel.WhenAnyValue(l => l.Profiles, l => l.SelectedProfile)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ =>
                {
                    _markMb = HfRamMark.ProfileMb(game.ListViewModel.SelectedProfile);
                    ShowRam();
                    RebuildMods();
                }));
            d.Add(game.WhenAnyValue(g => g.IsModsButtonVisible, g => g.IsProcessing)
                .CombineLatest(game.ListViewModel.WhenAnyValue(l => l.SelectedProfile))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(x =>
                {
                    var ((_, busy), selected) = x;
                    CanMods = !busy && game.BackendIsActive;
                    CanReinstall = HfOverviewModel.CanReinstallWith(selected is not null, busy, game.BackendIsActive);
                    RebuildMods();
                }));
        }
        return d;
    }

    private void ShowRam()
    {
        if (!_loaded || _applying)
            return;
        var auto = Page.DynamicRamValue && _markMb is not null;
        var mark = MarkOnSlider;
        var ram = auto ? mark!.Value : Page.RamValue;
        if (Math.Abs(ram - _ram) >= 0.5)
        {
            _ram = ram;
            this.RaisePropertyChanged(nameof(Ram));
        }
        RamText = ProfileCatalog.RamLabel((int)Math.Round(auto ? _markMb!.Value : Page.RamValue));
        HasMark = mark is not null && Page.MaxRamValue > Page.MinRamValue;
        MarkFraction = mark is { } m ? HfRamMark.Fraction(m, Page.MinRamValue, Page.MaxRamValue) : 0;
    }

    /// <summary>The optional mods of the chosen version: the stock model loads them from GML (online only).</summary>
    private void RebuildMods()
    {
        var profile = Game is { IsModsButtonVisible: true, BackendIsActive: true } game ? game.ListViewModel.SelectedProfile : null;
        if (ReferenceEquals(profile, _modsFor))
            return;
        _modsFor = profile;
        ModRows.Clear();
        HasMods = false;
        Mods = profile is null || _gml is null || _system is null
            ? null
            : new ModsPageViewModel(Page.MainViewModel, profile, Game!.User, _gml, _system);
        _modsWatch.Disposable = Mods?.WhenAnyValue(m => m.ProfileOptionalMods, m => m.OptionalModsDetails)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(x =>
            {
                ModRows.Clear();
                foreach (var row in HfModRow.Build(x.Item1, x.Item2))
                    ModRows.Add(row);
                HasMods = ModRows.Count > 0;
            });
    }
}
