using System;
using System.IO;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Gml.Launcher.Core.HedgeFox;
using Gml.Launcher.Core.Services;
using Gml.Launcher.ViewModels.Pages;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Sentry;
using Splat;

namespace Gml.Launcher.ViewModels.HedgeFox;

/// <summary>
///     The profile page: the player's skin from our site drawn full-length by the stock front render
///     (<see cref="SkinViewer.GetFront" />, shown with nearest-neighbour), the nick, and the way to change the skin.
/// </summary>
public sealed class HfProfileModel : ReactiveObject, IHfScreenModel
{
    private readonly ISkinService? _skins;

    public HfProfileModel(ProfilePageViewModel page, ISkinService? skins = null)
    {
        Page = page;
        _skins = skins ?? Locator.Current.GetService<ISkinService>();
        Nick = page.LauncherUser.Name;
    }

    public ProfilePageViewModel Page { get; }
    public string Nick { get; }

    [Reactive] public Bitmap? Body { get; private set; }
    [Reactive] public bool HasNoSkin { get; private set; }

    public IDisposable Start()
    {
        _ = LoadAsync();
        return Disposable.Empty;
    }

    private async Task LoadAsync()
    {
        if (_skins is null || Body is not null)
            return;
        try
        {
            var skin = await _skins.GetAsync(Page.LauncherUser.Uuid);
            // 16×32 for a 64 px skin: the screen scales it with nearest-neighbour
            var front = await Task.Run(() => SkinViewer.GetFront(new MemoryStream(skin.Png), 16));
            RxApp.MainThreadScheduler.Schedule(() =>
            {
                Body = new Bitmap(new MemoryStream(front));
                HasNoSkin = skin.IsFallback;
            });
        }
        catch (Exception exception)
        {
            SentrySdk.CaptureException(exception);
        }
    }
}
