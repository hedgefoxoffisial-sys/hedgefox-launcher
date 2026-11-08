using System.Net.Http;
using Gml.Client.Interfaces;
using Gml.Client.Models;
using Gml.Launcher.Core.HedgeFox;
using Xunit;

namespace HedgeFox.Launcher.Tests;

/// <summary>
///     Sign-in failures in Russian on the form, never the raw .NET text (copy-lore-4): the guard sits between the
///     stock login page and the GML client and turns failures into the «Details» the page puts on the form.
/// </summary>
public class LoginGuardTests
{
    private static readonly Task<(ILauncherUser User, string Message, IEnumerable<string> Details)> NoAnswer =
        Task.FromResult<(ILauncherUser, string, IEnumerable<string>)>((new AuthLauncherUser(), "", []));

    private static (IGmlClientManager Guard, TestProxy Inner) Over(
        Func<string, object?[]?, Task<(ILauncherUser User, string Message, IEnumerable<string> Details)>> auth)
    {
        var inner = TestProxy.Create<IGmlClientManager>((m, a) => m.Name switch
        {
            "Auth" or "AuthWith2Fa" => auth(m.Name, a),
            "get_InstallationDirectory" => "/games/hedgefox",
            "ChangeInstallationFolder" => throw new IOException("disk"),
            _ => null
        });
        return (HfAuthGuard.Wrap(inner), TestProxy.Of(inner));
    }

    private static Task<(ILauncherUser User, string Message, IEnumerable<string> Details)> Answer(
        bool isAuth, bool has2Fa = false, string message = "", params string[] details) =>
        Task.FromResult<(ILauncherUser, string, IEnumerable<string>)>(
            (new AuthLauncherUser { Name = "Ёжик", IsAuth = isAuth, Has2Fa = has2Fa }, message, details));

    [Fact]
    public async Task NoNetworkIsSaidInRussianOnTheForm()
    {
        var (guard, _) = Over((_, _) => throw new HttpRequestException("No such host is known. (api.hedgefox.ru:443)"));
        var r = await guard.Auth("Ёжик", "secret", "hwid");
        Assert.False(r.User.IsAuth);
        Assert.False(r.User.Has2Fa);
        Assert.Equal(HfStrings.NoConnection, r.Message);
        Assert.Equal([HfStrings.NoConnection], r.Details);
    }

    [Fact]
    public async Task AFailedTaskIsCaughtToo()
    {
        var (guard, _) = Over((_, _) => Task.FromException<(ILauncherUser, string, IEnumerable<string>)>(
            new HttpRequestException("nodename nor servname provided")));
        var r = await guard.Auth("Ёжик", "secret", "hwid");
        Assert.Equal([HfStrings.NoConnection], r.Details);
    }

    [Fact]
    public async Task ATimeoutSaysTheServerIsSlow()
    {
        var (guard, _) = Over((_, _) => throw new TaskCanceledException(
            "The request was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing."));
        var r = await guard.Auth("Ёжик", "secret", "hwid");
        Assert.Equal([HfStrings.TooSlow], r.Details);
    }

    [Fact]
    public async Task ARefusalWithoutDetailsIsAWrongLogin()
    {
        var (guard, _) = Over((_, _) => Answer(false, message: "Invalid credentials"));
        var r = await guard.Auth("Ёжик", "wrong", "hwid");
        Assert.Equal(HfStrings.WrongLogin, r.Message);
        Assert.Equal([HfStrings.WrongLogin], r.Details);
    }

    [Fact]
    public async Task SuccessAndTheSecondStepPassThrough()
    {
        var (ok, _) = Over((_, _) => Answer(true));
        Assert.True((await ok.Auth("Ёжик", "secret", "hwid")).User.IsAuth);

        var (twoFactor, _) = Over((_, _) => Answer(false, has2Fa: true, message: "2FA required"));
        var r = await twoFactor.Auth("Ёжик", "secret", "hwid");
        Assert.True(r.User.Has2Fa);
        Assert.Empty(r.Details);
    }

    [Fact]
    public async Task AWrongCodeIsSaidOnTheForm()
    {
        var (guard, _) = Over((_, _) => Answer(false, message: "Invalid 2FA code"));
        var r = await guard.AuthWith2Fa("Ёжик", "secret", "hwid", "123456");
        Assert.Equal(HfStrings.TwoFactorWrong, r.Message);
        Assert.Equal([HfStrings.TwoFactorWrong], r.Details);
    }

    [Fact]
    public async Task ServerDetailsAreKept()
    {
        var (guard, _) = Over((_, _) => Answer(false, message: "x", details: "Аккаунт заблокирован"));
        Assert.Equal(["Аккаунт заблокирован"], (await guard.Auth("Ёжик", "secret", "hwid")).Details);
    }

    [Theory]
    [InlineData("", "secret")]
    [InlineData("Ёжик", "")]
    [InlineData("  ", " ")]
    public async Task AnEmptyFormAsksForBothFieldsWithoutARequest(string login, string password)
    {
        var (guard, inner) = Over((_, _) => NoAnswer);
        var r = await guard.Auth(login, password, "hwid");
        Assert.Equal([HfStrings.FillLogin], r.Details);
        Assert.DoesNotContain("Auth", inner.Calls);
    }

    [Fact]
    public async Task EverythingElseGoesStraightThrough()
    {
        var (guard, inner) = Over((_, _) => NoAnswer);
        Assert.Equal("/games/hedgefox", guard.InstallationDirectory);
        // a failure of another member surfaces as itself, not wrapped by the proxy
        Assert.Throws<IOException>(() => guard.ChangeInstallationFolder("/x"));
        await guard.Auth("token");
        Assert.Contains("Auth", inner.Calls);
    }
}
