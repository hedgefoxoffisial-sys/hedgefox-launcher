using System;
using System.IO;
using System.Reactive;
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
///     (<see cref="SkinViewer.GetFront" />, shown with nearest-neighbour), the nick, the way to change the skin, and
///     «Выйти из аккаунта», which resets the router to the sign-in page.
/// </summary>
public sealed class HfProfileModel : ReactiveObject, IHfScreenModel
{
    private readonly ISkinService? _skins;
    private readonly IStorageService? _storage;

    public HfProfileModel(ProfilePageViewModel page, ISkinService? skins = null)
    {
        Page = page;
        _skins = skins ?? Locator.Current.GetService<ISkinService>();
        _storage = Locator.Current.GetService<IStorageService>();
        Nick = page.LauncherUser.Name;
        LogoutCommand = ReactiveCommand.CreateFromTask(LogoutAsync);
    }

    public ProfilePageViewModel Page { get; }
    public string Nick { get; }
    public ReactiveCommand<Unit, Unit> LogoutCommand { get; }

    [Reactive] public Bitmap? Body { get; private set; }
    [Reactive] public bool HasNoSkin { get; private set; }

    public IDisposable Start()
    {
        _ = LoadAsync();
        return Disposable.Empty;
    }

    // the main screen locks this page while the game downloads or runs, so no switch of accounts mid-game (code-8)
    private Task LogoutAsync()
    {
        if (_storage is null || Page.HostScreen is not MainWindowViewModel main)
            return Task.CompletedTask;
        return HfSession.LogoutAsync(_storage, main.Router, () => new LoginPageViewModel(main, main.OnClosed));
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
