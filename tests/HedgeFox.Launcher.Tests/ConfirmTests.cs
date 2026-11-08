using System.Reactive.Linq;
using System.Reactive.Subjects;
using Gml.Launcher.ViewModels.HedgeFox;
using Xunit;

namespace HedgeFox.Launcher.Tests;

/// <summary>
///     «Переустановить» deletes the profile's game folder (options, screenshots, packs), so it asks first and is off
///     while nothing can be downloaded again (code-1).
/// </summary>
public class ConfirmTests
{
    [Fact]
    public async Task TheFirstClickOnlyAsks()
    {
        var done = 0;
        var confirm = new HfConfirmModel(() => done++, Observable.Return(true));

        await confirm.AskCommand.Execute();
        Assert.True(confirm.IsAsking);
        Assert.Equal(0, done);

        await confirm.ConfirmCommand.Execute();
        Assert.Equal(1, done);
        Assert.False(confirm.IsAsking);
    }

    [Fact]
    public async Task CancelDoesNothing()
    {
        var done = 0;
        var confirm = new HfConfirmModel(() => done++, Observable.Return(true));
        await confirm.AskCommand.Execute();
        await confirm.CancelCommand.Execute();
        Assert.False(confirm.IsAsking);
        Assert.Equal(0, done);
    }

    [Fact]
    public async Task NothingRunsWithoutTheQuestion()
    {
        var confirm = new HfConfirmModel(() => throw new InvalidOperationException(), Observable.Return(true));
        Assert.False(await confirm.ConfirmCommand.CanExecute.FirstAsync());
    }

    [Fact]
    public async Task WhenItMustNotRunTheQuestionCloses()
    {
        var allowed = new BehaviorSubject<bool>(true);
        var confirm = new HfConfirmModel(() => { }, allowed);
        await confirm.AskCommand.Execute();

        allowed.OnNext(false); // a download started, or the sign-in server is gone
        Assert.False(confirm.IsAsking);
        Assert.False(await confirm.AskCommand.CanExecute.FirstAsync());
        Assert.False(await confirm.ConfirmCommand.CanExecute.FirstAsync());
    }

    [Theory]
    [InlineData(true, false, true, true)]
    [InlineData(false, false, true, false)] // no profile
    [InlineData(true, true, true, false)] // downloading or playing
    [InlineData(true, false, false, false)] // offline: the files could not be downloaded again
    public void ReinstallNeedsAProfileIdlenessAndTheSignInServer(bool profile, bool busy, bool online, bool can) =>
        Assert.Equal(can, HfOverviewModel.CanReinstallWith(profile, busy, online));
}
