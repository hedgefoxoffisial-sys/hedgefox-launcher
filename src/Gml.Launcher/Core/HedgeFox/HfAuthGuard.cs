using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Gml.Client.Interfaces;
using Gml.Client.Models;

namespace Gml.Launcher.Core.HedgeFox;

/// <summary>
///     Sits between the stock login page and the GML client (registered over it by <see cref="HedgeFoxServices" />)
///     and keeps sign-in failures in Russian. The stock page puts an answer's «Details» on the form and shows a raw
///     exception's English text in a toast; here every failure comes back as Russian Details instead: no network,
///     a slow server, a refused login, a wrong 2FA code, an empty form (no request then). Success, the «2FA needed»
///     answer and every other member go straight through.
/// </summary>
public class HfAuthGuard : DispatchProxy
{
    private IGmlClientManager _inner = null!;

    public static IGmlClientManager Wrap(IGmlClientManager inner)
    {
        var proxy = Create<IGmlClientManager, HfAuthGuard>();
        ((HfAuthGuard)(object)proxy)._inner = inner;
        return proxy;
    }

    /// <summary>What to tell the player when the request itself failed.</summary>
    public static string ForException(Exception exception) => exception switch
    {
        TimeoutException or OperationCanceledException => HfStrings.TooSlow,
        _ => HfStrings.NoConnection
    };

    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method is null)
            return null;
        args ??= [];
        switch (method.Name)
        {
            // Auth(login, password, hwid); Auth(accessToken) is the saved session and goes through
            case nameof(IGmlClientManager.Auth) when args.Length == 3:
                return Guard(() => _inner.Auth(Text(args[0]), Text(args[1]), Text(args[2])), args, false);
            case nameof(IGmlClientManager.AuthWith2Fa):
                return Guard(() => _inner.AuthWith2Fa(Text(args[0]), Text(args[1]), Text(args[2]), Text(args[3])), args,
                    !string.IsNullOrEmpty(Text(args[3])));
        }

        try
        {
            return method.Invoke(_inner, args);
        }
        catch (TargetInvocationException e) when (e.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(e.InnerException).Throw();
            throw;
        }
    }

    private static string Text(object? arg) => arg as string ?? string.Empty;

    private static async Task<(ILauncherUser User, string Message, IEnumerable<string> Details)> Guard(
        Func<Task<(ILauncherUser User, string Message, IEnumerable<string> Details)>> call, object?[] args,
        bool twoFactorStep)
    {
        var login = Text(args[0]);
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(Text(args[1])))
            return Refused(login, HfStrings.FillLogin);

        (ILauncherUser User, string Message, IEnumerable<string> Details) answer;
        try
        {
            answer = await call();
        }
        catch (Exception exception)
        {
            Sentry.SentrySdk.CaptureException(exception);
            return Refused(login, ForException(exception));
        }

        if (answer.User is { IsAuth: true })
            return answer;
        // the first step's «2FA needed» is not a failure: the page shows the code field
        if (!twoFactorStep && answer.User is { Has2Fa: true })
            return answer;
        var details = answer.Details?.Where(d => !string.IsNullOrWhiteSpace(d)).ToList() ?? [];
        if (details.Count > 0)
            return answer with { Details = details };
        var reason = twoFactorStep ? HfStrings.TwoFactorWrong : HfStrings.WrongLogin;
        return (answer.User ?? new AuthLauncherUser { Name = login }, reason, [reason]);
    }

    private static (ILauncherUser User, string Message, IEnumerable<string> Details) Refused(string login, string reason) =>
        (new AuthLauncherUser { Name = login }, reason, [reason]);
}
