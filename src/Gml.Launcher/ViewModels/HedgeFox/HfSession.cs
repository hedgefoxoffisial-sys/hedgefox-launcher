using System;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Gml.Launcher.Core;
using Gml.Launcher.Core.Services;
using GmlCore.Interfaces.User;
using ReactiveUI;
using Sentry;

namespace Gml.Launcher.ViewModels.HedgeFox;

public static class HfSession
{
    /// <summary>
    ///     «Выйти»: forgets the saved user and leaves only the login page in the router, so «назад» never returns
    ///     to the signed-in screens (the stock logout pushed the login page on top of them).
    /// </summary>
    public static async Task LogoutAsync(IStorageService storage, RoutingState router, Func<IRoutableViewModel> login)
    {
        await storage.SetAsync<IUser?>(StorageConstants.User, null);
        await router.NavigateAndReset.Execute(login());
    }

    /// <summary>Opens a web page in the player's browser (http and https only).</summary>
    public static void OpenLink(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            return;
        try
        {
            Process.Start(new ProcessStartInfo { FileName = uri.AbsoluteUri, UseShellExecute = true });
        }
        catch (Exception exception)
        {
            SentrySdk.CaptureException(exception);
        }
    }
}
