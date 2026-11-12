using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Gml.Launcher.Core.HedgeFox;
using Gml.Launcher.Core.Services;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Sentry;

namespace Gml.Launcher.ViewModels.HedgeFox;

/// <summary>One column of «Давай настроим лаунчер под тебя»: the version, its picture and lines, the advice.</summary>
public sealed class HfWelcomeColumn : ReactiveObject
{
    private static readonly Dictionary<string, Bitmap?> Loaded = new();

    public HfWelcomeColumn(HfProfileCard card, int index)
    {
        Card = card;
        Index = index;
        Points = HfWelcome.Points(card.GmlName, card.Ram);
        Picture = Load(HfWelcome.Picture(card.GmlName));
    }

    public HfProfileCard Card { get; }
    public string Title => Card.Title.ToUpperInvariant();

    /// <summary>0, 1, 2…: the column comes in a little after the one on its left.</summary>
    public int Index { get; }

    public IReadOnlyList<string> Points { get; }

    /// <summary>The version's own one line when the launcher has no lines for it.</summary>
    public bool ShowHint => Points.Count == 0 && Card.HasHint;
    public Bitmap? Picture { get; }
    public bool Recommended => Card.Recommended;

    [Reactive] public bool IsChosen { get; set; }

    private static Bitmap? Load(string uri)
    {
        lock (Loaded)
        {
            if (Loaded.TryGetValue(uri, out var cached))
                return cached;
            Bitmap? bitmap = null;
            try
            {
                using var stream = AssetLoader.Open(new Uri(uri));
                bitmap = new Bitmap(stream);
            }
            catch (Exception exception)
            {
                SentrySdk.CaptureException(exception);
            }
            Loaded[uri] = bitmap;
            return bitmap;
        }
    }
}

/// <summary>«Сравнить версии» in Настройки asks «Играть» to show the columns again.</summary>
public static class HfWelcomeRequest
{
    private static readonly Subject<Unit> Asked = new();
    private static bool _pending;

    public static IObservable<Unit> Requests => Asked;

    public static void Ask()
    {
        _pending = true;
        Asked.OnNext(Unit.Default);
    }

    /// <summary>True once per <see cref="Ask" /> that nobody has shown yet.</summary>
    public static bool Take()
    {
        var was = _pending;
        _pending = false;
        return was;
    }
}

/// <summary>
///     «Давай настроим лаунчер под тебя» over «Играть» (<see cref="HfWelcome" />): after the first login, once, three
///     columns Слабый ПК / Обычный / Шейдеры come in one after another; the advised one carries «ДЛЯ ТВОЕГО ПК» and is
///     chosen at first. «ПОЕХАЛИ» (or Escape) keeps the chosen version, as a pick in the bottom bar would.
/// </summary>
public sealed class HfWelcomeModel : ReactiveObject
{
    private readonly HfVersionPicker _picker;
    private readonly IStorageService? _storage;
    private bool _seen = true;
    private bool _shownThisRun;

    public HfWelcomeModel(HfVersionPicker picker, IStorageService? storage)
    {
        _picker = picker;
        _storage = storage;
        ChooseCommand = ReactiveCommand.Create<HfWelcomeColumn>(Choose);
        GoCommand = ReactiveCommand.CreateFromTask(GoAsync);
    }

    public ObservableCollection<HfWelcomeColumn> Columns { get; } = [];

    [Reactive] public bool IsOpen { get; private set; }

    /// <summary>«Твой ПК: NVIDIA GeForce RTX 3060 · 16 ГБ памяти»; empty until the probe answers.</summary>
    [Reactive] public string PcLine { get; private set; } = string.Empty;
    [Reactive] public bool HasPcLine { get; private set; }

    public ReactiveCommand<HfWelcomeColumn, Unit> ChooseCommand { get; }
    public ReactiveCommand<Unit, Unit> GoCommand { get; }

    public IDisposable Start()
    {
        var d = new CompositeDisposable();
        // the versions and the advice arrive at different moments: open once both have had their chance
        d.Add(_picker.WhenAnyValue(p => p.HasVersions, p => p.Advice, p => p.Hardware)
            .Throttle(TimeSpan.FromMilliseconds(400))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => Refresh()));
        d.Add(HfWelcomeRequest.Requests
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => Refresh()));
        _ = LoadSeenAsync();
        return d;
    }

    /// <summary>Shows the columns now (the screenshot harness).</summary>
    public void Open()
    {
        var chosen = Columns.FirstOrDefault(c => c.IsChosen)?.Card.GmlName;
        Columns.Clear();
        var cards = HfWelcome.Order(_picker.Versions);
        chosen ??= cards.FirstOrDefault(c => c.Selected)?.GmlName ?? cards.FirstOrDefault(c => c.Recommended)?.GmlName;
        for (var i = 0; i < cards.Count; i++)
            Columns.Add(new HfWelcomeColumn(cards[i], i)
            {
                IsChosen = string.Equals(cards[i].GmlName, chosen, StringComparison.Ordinal)
            });
        if (Columns.Count > 0 && !Columns.Any(c => c.IsChosen))
            Columns[0].IsChosen = true;
        var line = HfWelcome.PcLine(_picker.Hardware);
        PcLine = line.Length > 0 ? "Твой ПК: " + line : string.Empty;
        HasPcLine = PcLine.Length > 0;
        IsOpen = true;
        _shownThisRun = true;
    }

    private void Refresh()
    {
        if (!_picker.HasVersions || !_picker.CanSwitch)
            return;
        if (HfWelcomeRequest.Take() || (!_seen && !_shownThisRun))
            Open();
        else if (IsOpen)
            Open(); // the advice came after the columns: put the badge and the line in
    }

    private void Choose(HfWelcomeColumn column)
    {
        foreach (var c in Columns)
            c.IsChosen = ReferenceEquals(c, column);
    }

    private async Task GoAsync()
    {
        var chosen = Columns.FirstOrDefault(c => c.IsChosen);
        IsOpen = false;
        _seen = true;
        try
        {
            if (chosen is not null && !chosen.Card.Selected)
                await _picker.SelectCommand.Execute(chosen.Card);
            if (_storage is not null)
                await _storage.SetAsync(HfWelcome.SeenKey, "1");
        }
        catch (Exception exception)
        {
            SentrySdk.CaptureException(exception);
        }
    }

    private async Task LoadSeenAsync()
    {
        var seen = true;
        try
        {
            seen = _storage is null || await _storage.GetAsync<string>(HfWelcome.SeenKey) == "1";
        }
        catch (Exception exception)
        {
            SentrySdk.CaptureException(exception);
        }
        RxApp.MainThreadScheduler.Schedule(() =>
        {
            _seen = seen;
            Refresh();
        });
    }
}
