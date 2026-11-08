// Renders the launcher's windows and pages to PNG offscreen (Avalonia.Headless + Skia, 1x), with fake services and
// fixed data: no network, no ~/.hedgefox, the same picture on every run. See HEDGEFOX.md «Снимки экранов».
//
//   dotnet run --project tools/shots -c Release -- <out-dir>
//
// Every capture waits on a condition (page shown, data loaded, layout settled), never on a fixed sleep.
// Screens are drawn at the default 1180×720 and at the minimum 1000×620 window size.
using System.Collections.ObjectModel;
using System.Reflection;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using Avalonia.VisualTree;
using GamerVII.Notification.Avalonia;
using GamerVII.Notification.Avalonia.Controls;
using Gml.Launcher;
using Gml.Launcher.Core;
using Gml.Launcher.Core.HedgeFox;
using Gml.Launcher.Core.Services;
using Gml.Launcher.ViewModels;
using Gml.Launcher.ViewModels.HedgeFox;
using Gml.Launcher.ViewModels.Pages;
using Gml.Launcher.Views;
using Gml.Launcher.Views.HedgeFox;
using Gml.Launcher.Views.HedgeFox.Controls;
using Gml.Launcher.Views.SplashScreen;
using HedgeFox.Shots;
using ReactiveUI;

if (args.Length < 1)
{
    Console.Error.WriteLine("usage: HedgeFox.Shots <out-dir>");
    return 2;
}

var outDir = Path.GetFullPath(args[0]);
Directory.CreateDirectory(outDir);
// what the fakes report as the app folder; nothing is written there
var scratch = Path.Combine(Path.GetTempPath(), "hedgefox-shots");
// the stock mods page creates «<install dir>/game data/<profile>/mods»; the fakes' Windows-style install dir is a
// relative path on macOS and Linux, so it must land in the scratch folder, never in the working tree
Directory.CreateDirectory(scratch);
Environment.CurrentDirectory = scratch;
var clock = System.Diagnostics.Stopwatch.StartNew();

AppBuilder.Configure<App>()
    .UseSkia()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .WithInterFont()
    .UseReactiveUI()
    .SetupWithoutStarting();
Fakes.Register(scratch);
HfServerInfoModel.Clock = () => Fakes.Now;

var shots = new List<string>();

void Save(TopLevel top, string name)
{
    Ui.Settle(top);
    Ui.WaitForFades(top);
    var frame = top.CaptureRenderedFrame() ?? throw new InvalidOperationException($"{name}: no frame");
    var path = Path.Combine(outDir, name + ".png");
    frame.Save(path);
    shots.Add(path);
    Console.WriteLine($"{name}: {frame.PixelSize.Width}x{frame.PixelSize.Height} ({clock.ElapsedMilliseconds} ms)");
}

// 1. Theme gallery: every primitive and its states
var gallery = new ThemeGallery();
var galleryWindow = new Window { Width = 1180, Height = 720, Content = gallery };
galleryWindow.Show();
Ui.Settle(galleryWindow);
gallery.ForceStates();
Save(galleryWindow, "theme-gallery");
galleryWindow.Close();

// 2. Splash, before and after its checks (all answered by the fakes); the cogs hold still for the picture
var splashVm = new SplashScreenViewModel();
var splash = new SplashScreen { DataContext = splashVm };
splash.Show();
Ui.Settle(splash);
foreach (var gears in splash.GetVisualDescendants().OfType<HfGears>())
    gears.IsSpinning = false;
Save(splash, "splash");
// Gml.Client prints the failed Sentry lookup (the fakes point it at a closed local port): keep the log readable
var stdout = Console.Out;
Console.SetOut(TextWriter.Null);
var init = splashVm.InitializeAsync();
Ui.PumpUntil(() => init.IsCompleted, "splash checks");
Console.SetOut(stdout);
Save(splash, "splash-ready");
// the sign-in server does not answer: the stock check's status replaces the hints
splashVm.StatusText = splashVm.LocalizationService.GetString(SystemConstants.BackendOffline);
Ui.PumpUntil(() => splash.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == HfStrings.SplashOffline),
    "splash offline line");
Save(splash, "splash-offline");
splash.Close();

// 3. The screens with the Windows frame (own pixel buttons), at the default and at the minimum size
HfWindowChrome.Override = HfChromeKind.Windows;
foreach (var (w, h) in new[] { (1180, 720), (1000, 620) })
    Screens(w, h, $"-{w}x{h}");

// 4. macOS frame: the traffic lights' inset in the title bar
HfWindowChrome.Override = HfChromeKind.MacOs;
var (mac, macVm) = OpenMain(1180, 720);
Ui.PumpUntil(() => macVm.Router.GetCurrentViewModel() is LoginPageViewModel, "macOS login page");
Save(mac, "login-macos-1180x720");
mac.Close();

Console.WriteLine($"{shots.Count} shots in {outDir} ({clock.ElapsedMilliseconds} ms)");
return 0;

void Screens(int width, int height, string size)
{
    Fakes.StatusNow = Fakes.Status;
    Fakes.NewsFails = false;
    var (main, vm) = OpenMain(width, height);

    // login: empty form, then a typed nick, then the 2FA step with an error
    Ui.PumpUntil(() => vm.Router.GetCurrentViewModel() is LoginPageViewModel, "login page");
    var login = (LoginPageViewModel)vm.Router.GetCurrentViewModel()!;
    Ui.PumpUntil(() => HfModel<HfLoginModel>(main)?.Info.News.Count == 3, "login news");
    Save(main, "login" + size);
    login.Login = Fakes.User.Name;
    login.Is2FaVisible = true;
    login.Errors = new ObservableCollection<string> { HfStrings.TwoFactorWrong };
    Save(main, "login-2fa" + size);

    // our API not deployed yet: «Нет данных» and the news error, never a «0»
    Fakes.StatusNow = HfStatusSnapshot.NoData;
    Fakes.NewsFails = true;
    var bare = new LoginPageViewModel(vm, vm.OnClosed);
    vm.Router.NavigateAndReset.Execute(bare).Subscribe();
    Ui.PumpUntil(() => HfModel<HfLoginModel>(main) is { Page: var p, Info.HasNewsMessage: true } && p == bare, "login without data");
    Save(main, "login-nodata" + size);
    Fakes.StatusNow = Fakes.Status;
    Fakes.NewsFails = false;

    // main screen
    var overview = new OverviewPageViewModel(vm, Fakes.User, vm.OnClosed);
    vm.Router.NavigateAndReset.Execute(overview).Subscribe();
    Ui.PumpUntil(() => overview.ListViewModel.Profiles?.Count == 3 && overview.ListViewModel.SelectedProfile != null,
        "overview profiles");
    Ui.PumpUntil(() => HfModel<HfOverviewModel>(main) is { Face: not null, Cards.Count: 3 } m && m.Info.News.Count > 0,
        "overview face, cards and news");
    Save(main, "overview" + size);

    // toasts as the stock view models queue them (their fills only pick the left bar): an error, a success, the VPN
    // note and «reconnecting» with its overlay bar
    if (width == 1180)
    {
        var text = new ResourceLocalizationService();
        var toasts = new[]
        {
            vm.Manager.CreateMessage(true, "#D03E3E", text.GetString(SystemConstants.InvalidAuthData), HfStrings.NoConnection),
            vm.Manager.CreateMessage(true, "#008C45", text.GetString(SystemConstants.Success), text.GetString(SystemConstants.ModEnabled)),
            vm.Manager.CreateMessage(true, "#3684EA", text.GetString(SystemConstants.Information), text.GetString(SystemConstants.VpnUse)),
            vm.Manager.CreateMessage().Background("#111111")
                .HasHeader(text.GetString(SystemConstants.LostConnection))
                .HasMessage(text.GetString(SystemConstants.Reconnecting))
                .WithOverlay(new ProgressBar
                {
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom, Height = 2, IsIndeterminate = true,
                    IsHitTestVisible = false
                })
        }.Select(b => b.Queue()).ToList();
        Ui.PumpUntil(() => main.GetVisualDescendants().OfType<NotificationMessage>().Count() == toasts.Count, "toasts");
        Save(main, "overview-toasts" + size);
        foreach (var t in toasts)
            vm.Manager.Dismiss(t);
        Ui.PumpUntil(() => !main.GetVisualDescendants().OfType<NotificationMessage>().Any(), "toasts gone");
    }

    // downloading: the meter, the step and the speed under «ИГРАТЬ»
    overview.Headline = "Обновление";
    overview.Description = "Осталось: 312 файлов";
    overview.LoadingPercentage = 42;
    overview.LoadingSpeed = "3.5 MB/s";
    overview.IsProcessing = true;
    Save(main, "overview-download" + size);
    overview.IsProcessing = false;

    foreach (var (command, name) in new[]
             {
                 (overview.GoSettingsCommand, "settings"), (overview.GoProfileCommand, "profile"),
                 (overview.GoModsCommand, "mods")
             })
    {
        command.Execute(null);
        Ui.PumpUntil(() => vm.Router.GetCurrentViewModel() != overview, name + " page");
        if (name == "settings")
        {
            // the stock page loads the saved settings on the thread pool; then the slider is switched to manual
            var settings = (SettingsPageViewModel)vm.Router.GetCurrentViewModel()!;
            Ui.PumpUntil(() => settings.InstallationFolder != null && settings.MaxRamValue > 0, "saved settings");
            settings.DynamicRamValue = false;
            settings.RamValue = 6144;
            Ui.PumpUntil(() => HfModel<HfSettingsModel>(main) is { HasMark: true, IsManualRam: true }, "settings and the mark");
        }
        if (name == "profile")
            Ui.PumpUntil(() => HfModel<HfProfileModel>(main) is { Body: not null }, "skin render");
        Save(main, name + size);
        vm.Router.NavigateBack.Execute().Subscribe();
        Ui.PumpUntil(() => vm.Router.GetCurrentViewModel() == overview, "back to overview");
    }

    // the server asleep, then no data at all
    Fakes.StatusNow = new HfStatusSnapshot(HfStatusKind.Offline, CheckedAt: Fakes.Status.CheckedAt);
    var asleep = new OverviewPageViewModel(vm, Fakes.User, vm.OnClosed);
    vm.Router.NavigateAndReset.Execute(asleep).Subscribe();
    Ui.PumpUntil(() => asleep.ListViewModel.Profiles?.Count == 3, "overview, server asleep");
    Save(main, "overview-asleep" + size);
    Fakes.StatusNow = HfStatusSnapshot.NoData;
    Fakes.NewsFails = true;
    var nodata = new OverviewPageViewModel(vm, Fakes.User, vm.OnClosed);
    vm.Router.NavigateAndReset.Execute(nodata).Subscribe();
    Ui.PumpUntil(() => nodata.ListViewModel.Profiles?.Count == 3, "overview without data");
    Save(main, "overview-nodata" + size);
    Fakes.StatusNow = Fakes.Status;
    Fakes.NewsFails = false;
    main.Close();

    // the sign-in server was down at start: offline mode, «Войти» is off and says why, the stock toast comes in 2 s
    Fakes.BackendOffline = true;
    var (offline, offlineVm) = OpenMain(width, height);
    Ui.PumpUntil(() => offlineVm.Router.GetCurrentViewModel() is LoginPageViewModel, "offline login page");
    // the stock check runs 2 s after start on a dispatcher timer, which the headless loop does not fire
    typeof(MainWindowViewModel).GetMethod("CheckBackend", BindingFlags.NonPublic | BindingFlags.Instance)!
        .Invoke(offlineVm, null);
    Ui.PumpUntil(() => offline.GetVisualDescendants().OfType<NotificationMessage>().Any(), "offline toast", 10000);
    Save(offline, "login-offline" + size);
    offline.Close();
    Fakes.BackendOffline = false;
}

// the HedgeFox model of the page on screen
static T? HfModel<T>(Window window) where T : class =>
    window.GetVisualDescendants().OfType<Control>().Where(c => c.Name == "Root").Select(c => c.DataContext).OfType<T>()
        .FirstOrDefault();

static (MainWindow Window, MainWindowViewModel Vm) OpenMain(int width, int height)
{
    var vm = new MainWindowViewModel();
    var window = new MainWindow { DataContext = vm, Width = width, Height = height };
    window.Show();
    // page changes fade in the app; a capture must not land in the middle of one
    Ui.Settle(window);
    window.GetVisualDescendants().OfType<RoutedViewHost>().Single().PageTransition = null;
    return (window, vm);
}

internal static class Ui
{
    /// <summary>Runs the UI loop until <paramref name="condition" /> holds; fails after the timeout.</summary>
    public static void PumpUntil(Func<bool> condition, string what, int timeoutMs = 20000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"waited {timeoutMs} ms for {what}");
            Tick();
            Thread.Sleep(10); // lets thread-pool continuations post back to the UI thread
        }
    }

    /// <summary>
    ///     Three render ticks in a row with a valid layout. The headless compositor shows a change one tick late,
    ///     so a capture right after a state change would miss it.
    /// </summary>
    public static void Settle(TopLevel top)
    {
        var quiet = 0;
        PumpUntil(() =>
        {
            Tick();
            quiet = top.IsMeasureValid && top.IsArrangeValid ? quiet + 1 : 0;
            return quiet >= 3;
        }, "layout to settle");
    }

    /// <summary>
    ///     Waits until no opacity in the tree changes between frames (the stock pages fade in, the app's own fades
    ///     run on the wall clock). The fog drifts but never fades, so it does not hold this up.
    /// </summary>
    public static void WaitForFades(TopLevel top)
    {
        double[]? last = null;
        var still = 0;
        PumpUntil(() =>
        {
            Tick();
            var now = top.GetVisualDescendants().Select(v => v.Opacity).ToArray();
            still = last is not null && now.SequenceEqual(last) ? still + 1 : 0;
            last = now;
            return still >= 3;
        }, "fades to finish");
        Settle(top);
    }

    private static void Tick()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }
}
