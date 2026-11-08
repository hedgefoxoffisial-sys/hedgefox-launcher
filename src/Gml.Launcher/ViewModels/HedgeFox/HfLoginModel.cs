using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Gml.Launcher.ViewModels.Pages;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace Gml.Launcher.ViewModels.HedgeFox;

/// <summary>
///     The login screen's own state next to the stock <see cref="LoginPageViewModel" /> (its commands, 2FA and
///     checks run unchanged): the status line (no news on this screen) and the first error line.
/// </summary>
public sealed class HfLoginModel : ReactiveObject, IHfScreenModel
{
    public HfLoginModel(LoginPageViewModel page, HfServerInfoModel? info = null)
    {
        Page = page;
        Info = info ?? HfServerInfoModel.FromServices(0);
        CancelTwoFactorCommand = ReactiveCommand.Create(() =>
        {
            Page.TwoFactorCode = string.Empty;
            Page.Is2FaVisible = false;
        });
    }

    public LoginPageViewModel Page { get; }
    public HfServerInfoModel Info { get; }
    public ReactiveCommand<Unit, Unit> CancelTwoFactorCommand { get; }

    [Reactive] public string? Error { get; private set; }
    [Reactive] public bool HasError { get; private set; }

    public IDisposable Start()
    {
        var d = new CompositeDisposable(Info.Start());
        // the stock page replaces or clears its error list; follow both
        ObservableCollection<string>? watched = null;
        void Update()
        {
            Error = watched?.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e));
            HasError = Error is not null;
        }

        void OnChanged(object? s, NotifyCollectionChangedEventArgs e) => Update();
        d.Add(Page.WhenAnyValue(p => p.Errors).Subscribe(list =>
        {
            if (watched is not null)
                watched.CollectionChanged -= OnChanged;
            watched = list;
            if (watched is not null)
                watched.CollectionChanged += OnChanged;
            Update();
        }));
        d.Add(Disposable.Create(() =>
        {
            if (watched is not null)
                watched.CollectionChanged -= OnChanged;
        }));
        return d;
    }
}
