using System;
using System.Collections.ObjectModel;
using System.Linq;
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

namespace Gml.Launcher.ViewModels.HedgeFox;

/// <summary>
///     «Версия игры» (Обычный / Слабый ПК / Шейдеры) for the main screen's stock profile list: the cards with the one
///     advised for this PC (<see cref="HardwareAdvisor" />), the chosen one, and the pick, remembered as the player's own
///     (<see cref="HfProfilePick" />). The bottom bar of «Играть» and Настройки show it; no switching while the game
///     downloads or runs (code-8).
/// </summary>
public sealed class HfVersionPicker : ReactiveObject
{
    private readonly OverviewPageViewModel _game;
    private readonly IHardwareProbe? _hardware;
    private readonly IStorageService? _storage;

    public HfVersionPicker(OverviewPageViewModel game, IHardwareProbe? hardware, IStorageService? storage)
    {
        _game = game;
        _hardware = hardware;
        _storage = storage;
        SelectCommand = ReactiveCommand.CreateFromTask<HfProfileCard>(SelectAsync, this.WhenAnyValue(x => x.CanSwitch));
        ToggleCommand = ReactiveCommand.Create(() => { IsOpen = !IsOpen; },
            this.WhenAnyValue(x => x.CanSwitch, x => x.HasVersions, (free, many) => free && many));
        CloseCommand = ReactiveCommand.Create(() => { IsOpen = false; });
    }

    public ObservableCollection<HfProfileCard> Versions { get; } = [];

    /// <summary>The hardware advice; empty until the probe answers.</summary>
    [Reactive] public HfAdvice Advice { get; private set; } = new(null, false);

    /// <summary>More than one version to choose from.</summary>
    [Reactive] public bool HasVersions { get; private set; }

    /// <summary>The chosen version, «Слабый ПК»; empty while the list loads.</summary>
    [Reactive] public string CurrentTitle { get; private set; } = string.Empty;
    [Reactive] public bool HasCurrent { get; private set; }

    /// <summary>One line about the chosen version, «Короче дальность, легче графика…».</summary>
    [Reactive] public string Hint { get; private set; } = string.Empty;
    [Reactive] public bool HasHint { get; private set; }

    /// <summary>The chosen version is the one advised for this PC.</summary>
    [Reactive] public bool CurrentIsAdvised { get; private set; }

    /// <summary>Nothing downloads or runs.</summary>
    [Reactive] public bool CanSwitch { get; private set; }

    /// <summary>The list under the bottom bar's button is open.</summary>
    [Reactive] public bool IsOpen { get; private set; }

    public ReactiveCommand<HfProfileCard, Unit> SelectCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleCommand { get; }
    public ReactiveCommand<Unit, Unit> CloseCommand { get; }

    public IDisposable Start()
    {
        var d = new CompositeDisposable();
        var list = _game.ListViewModel;
        d.Add(list.WhenAnyValue(l => l.Profiles, l => l.SelectedProfile)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => Rebuild()));
        d.Add(_game.WhenAnyValue(g => g.IsProcessing)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(busy =>
            {
                CanSwitch = !busy;
                if (busy)
                    IsOpen = false;
            }));
        _ = LoadAdviceAsync();
        return d;
    }

    private void Rebuild()
    {
        var list = _game.ListViewModel;
        var cards = HfProfileCards.Build(list.Profiles ?? [], Advice, list.SelectedProfile?.Name);
        Versions.Clear();
        foreach (var card in cards)
            Versions.Add(card);
        HasVersions = Versions.Count > 1;
        var current = cards.FirstOrDefault(c => c.Selected);
        CurrentTitle = current?.Title ?? string.Empty;
        HasCurrent = current is not null;
        Hint = current?.Hint ?? string.Empty;
        HasHint = Hint.Length > 0;
        CurrentIsAdvised = current?.Recommended == true;
    }

    private async Task SelectAsync(HfProfileCard card)
    {
        IsOpen = false;
        if (_game.IsProcessing)
            return;
        try
        {
            // the pick is in memory before the list changes: the main screen re-picks on every change of the list
            var remember = HfVersionChoice.SetAsync(_storage, HfProfilePick.Remember(card.GmlName, Advice));
            _game.ListViewModel.SelectedProfile = card.Profile;
            await remember;
        }
        catch (Exception exception)
        {
            SentrySdk.CaptureException(exception);
        }
    }

    private async Task LoadAdviceAsync()
    {
        if (_hardware is null)
            return;
        try
        {
            var advice = HardwareAdvisor.Advise(await _hardware.ReadAsync());
            RxApp.MainThreadScheduler.Schedule(() =>
            {
                Advice = advice;
                Rebuild();
            });
        }
        catch (Exception exception)
        {
            SentrySdk.CaptureException(exception);
        }
    }
}
