using System;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace Gml.Launcher.ViewModels.HedgeFox;

/// <summary>
///     An action that asks first: <see cref="AskCommand" /> opens the question, <see cref="ConfirmCommand" /> runs
///     the action once, <see cref="CancelCommand" /> closes it. While <c>allowed</c> is false nothing can be asked
///     or confirmed, and an open question closes.
/// </summary>
public sealed class HfConfirmModel : ReactiveObject
{
    public HfConfirmModel(Action act, IObservable<bool> allowed)
    {
        var can = allowed.StartWith(false).DistinctUntilChanged().Replay(1).RefCount();
        can.Where(ok => !ok).Subscribe(_ => IsAsking = false);

        AskCommand = ReactiveCommand.Create(() => { IsAsking = true; }, can);
        CancelCommand = ReactiveCommand.Create(() => { IsAsking = false; });
        ConfirmCommand = ReactiveCommand.Create(() =>
        {
            IsAsking = false;
            act();
        }, can.CombineLatest(this.WhenAnyValue(x => x.IsAsking), (ok, asking) => ok && asking));
    }

    [Reactive] public bool IsAsking { get; private set; }
    public ReactiveCommand<Unit, Unit> AskCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }
    public ReactiveCommand<Unit, Unit> ConfirmCommand { get; }
}
