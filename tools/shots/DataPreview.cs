using System.Reactive.Linq;
using Avalonia.Media.Imaging;
using Gml.Launcher.Core.HedgeFox;
using Gml.Launcher.Views.HedgeFox.Controls;
using ReactiveUI;
using Splat;

namespace HedgeFox.Shots;

public sealed record NewsCard(string Source, string Date, string Title, string Text, bool IsTelegram);

public sealed record ProfileCard(string Title, string Hint, string Ram, bool Recommended, bool Selected, bool ShadersAlsoFit);

/// <summary>
///     Not a launcher screen: a page inside the real shell that shows the HedgeFox services' fixed data with the
///     theme primitives, so the wiring (services → wording → controls) is visible before the screens exist.
/// </summary>
public sealed class DataPreviewViewModel(IScreen screen) : ReactiveObject, IRoutableViewModel
{
    public string UrlPathSegment => "data-preview";
    public IScreen HostScreen { get; } = screen;

    public string StatusLine { get; private set; } = "";
    public HfDotState Dot { get; private set; }
    public HfPelenaView? Pelena { get; private set; }
    public double PelenaPercent => (Pelena?.Fraction ?? 0) * 100;
    public string PelenaTrend => Pelena?.Trend is { } t ? "· " + t : "";
    public IReadOnlyList<NewsCard> News { get; private set; } = [];
    public IReadOnlyList<ProfileCard> Profiles { get; private set; } = [];
    public Bitmap? Face { get; private set; }
    public string Nick { get; private set; } = "";

    public async Task LoadAsync()
    {
        var status = await Get<IStatusService>().Updates.FirstAsync();
        StatusLine = HfStrings.StatusLine(status);
        Dot = status.Kind switch
        {
            HfStatusKind.Online => HfDotState.Online,
            HfStatusKind.Offline => HfDotState.Offline,
            HfStatusKind.Loading => HfDotState.Waiting,
            _ => HfDotState.Unknown
        };
        Pelena = HfPelenaView.From(status.Pelena);

        var news = await Get<INewsService>().LoadAsync();
        News = news.Items.Select(n => new NewsCard(HfStrings.NewsSource(n.Source).ToUpperInvariant(),
            HfStrings.NewsDate(n.Date, Fakes.Now), n.Title, n.Text, n.IsTelegram)).ToList();

        var advice = HardwareAdvisor.Advise(await Get<IHardwareProbe>().ReadAsync());
        Profiles = ProfileCatalog.Order(Fakes.Profiles(), p => p.Name).Select(p =>
        {
            var info = ProfileCatalog.For(p.Name, p.DisplayName, p.RecommendedRam);
            return new ProfileCard(info.Title, info.Hint, $"{HfStrings.MemoryForGame}: {ProfileCatalog.RamLabel(info.RamMb)}",
                info.GmlName == advice.Recommended, info.GmlName == ProfileCatalog.LowPc,
                info.GmlName == ProfileCatalog.Shaders && advice.ShadersOk);
        }).ToList();

        var skin = await Get<ISkinService>().GetAsync(Fakes.User.Uuid);
        Face = new Bitmap(new MemoryStream(HfSkinImages.Face(skin.Png, 1)));
        Nick = Fakes.User.Name;
    }

    private static T Get<T>() => Locator.Current.GetService<T>() ?? throw new InvalidOperationException(typeof(T).Name);
}

/// <summary>Resolves the preview page, everything else through the launcher's own locator.</summary>
public sealed class ShotsViewLocator(IViewLocator inner) : IViewLocator
{
    public IViewFor? ResolveView<T>(T? viewModel, string? contract = null) =>
        viewModel is DataPreviewViewModel vm ? new DataPreview { DataContext = vm, ViewModel = vm } : inner.ResolveView(viewModel, contract);
}
