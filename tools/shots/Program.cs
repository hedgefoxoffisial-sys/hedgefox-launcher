// Renders the launcher's windows and pages to PNG offscreen (Avalonia.Headless + Skia, 1x), with fake services and
// fixed data: no network, no ~/.hedgefox, the same picture on every run. See HEDGEFOX.md «Снимки экранов».
//
//   dotnet run --project tools/shots -c Release -- <out-dir>
//
// Every capture waits on a condition (page shown, data loaded, layout settled), never on a fixed sleep.
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Gml.Launcher;
using Gml.Launcher.Core.Helpers;
using Gml.Launcher.ViewModels;
using Gml.Launcher.ViewModels.Pages;
using Gml.Launcher.Views;
using Gml.Launcher.Views.HedgeFox;
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
var clock = System.Diagnostics.Stopwatch.StartNew();

AppBuilder.Configure<App>()
    .UseSkia()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .WithInterFont()
    .UseReactiveUI()
    .SetupWithoutStarting();
Fakes.Register(scratch);

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

// 2. Splash, before and after its checks (all answered by the fakes)
var splashVm = new SplashScreenViewModel();
var splash = new SplashScreen { DataContext = splashVm };
splash.Show();
Save(splash, "splash");
// Gml.Client prints the failed Sentry lookup (the fakes point it at a closed local port): keep the log readable
var stdout = Console.Out;
Console.SetOut(TextWriter.Null);
var init = splashVm.InitializeAsync();
Ui.PumpUntil(() => init.IsCompleted, "splash checks");
Console.SetOut(stdout);
Save(splash, "splash-ready");

// 3. Main window with the Windows frame (own pixel buttons): login, then the pages behind it
HfWindowChrome.Override = HfChromeKind.Windows;
var main = splash.GetMainWindow();
main.Width = 1180;
main.Height = 720;
main.Show();
NoPageFades(main);
splash.Close();
var mainVm = (MainWindowViewModel)main.DataContext!;
Ui.PumpUntil(() => mainVm.Router.GetCurrentViewModel() is LoginPageViewModel, "login page");
Save(main, "login");

var overview = new OverviewPageViewModel(mainVm, Fakes.User, mainVm.OnClosed);
mainVm.Router.Navigate.Execute(overview).Subscribe();
Ui.PumpUntil(() => overview.ListViewModel.Profiles?.Count == 3 && overview.ListViewModel.SelectedProfile != null,
    "overview profiles");
Save(main, "overview");

foreach (var (command, name) in new[]
         {
             (overview.GoSettingsCommand, "settings"), (overview.GoProfileCommand, "profile"),
             (overview.GoModsCommand, "mods")
         })
{
    command.Execute(null);
    Ui.PumpUntil(() => mainVm.Router.GetCurrentViewModel() != overview, name + " page");
    Save(main, name);
    mainVm.Router.NavigateBack.Execute().Subscribe();
    Ui.PumpUntil(() => mainVm.Router.GetCurrentViewModel() == overview, "back to overview");
}

// 4. The services' fixed data drawn with the theme inside the shell, with the Windows and the macOS frame
ShowPreview(main, mainVm);
Save(main, "data-preview");
main.Close();

HfWindowChrome.Override = HfChromeKind.MacOs;
var macVm = new MainWindowViewModel();
var mac = new MainWindow { DataContext = macVm, Width = 1180, Height = 720 };
mac.Show();
NoPageFades(mac);
Ui.PumpUntil(() => macVm.Router.GetCurrentViewModel() is LoginPageViewModel, "macOS login page");
Save(mac, "login-macos");
ShowPreview(mac, macVm);
Save(mac, "data-preview-macos");
mac.Close();

Console.WriteLine($"{shots.Count} shots in {outDir} ({clock.ElapsedMilliseconds} ms)");
return 0;

// Page changes fade in the app; a capture must not land in the middle of one.
static void NoPageFades(Window window)
{
    Ui.Settle(window);
    window.GetVisualDescendants().OfType<RoutedViewHost>().Single().PageTransition = null;
}

static void ShowPreview(Window window, MainWindowViewModel vm)
{
    var preview = new DataPreviewViewModel(vm);
    var load = preview.LoadAsync();
    Ui.PumpUntil(() => load.IsCompleted, "preview data");
    load.GetAwaiter().GetResult(); // rethrows a failed load
    var host = window.GetVisualDescendants().OfType<RoutedViewHost>().Single();
    host.ViewLocator = new ShotsViewLocator(new AppViewLocator());
    vm.Router.Navigate.Execute(preview).Subscribe();
    Ui.PumpUntil(() => host.Content is DataPreview, "preview page");
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
