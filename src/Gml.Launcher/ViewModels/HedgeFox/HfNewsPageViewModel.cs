using Gml.Launcher.ViewModels.Base;
using ReactiveUI;

namespace Gml.Launcher.ViewModels.HedgeFox;

/// <summary>
///     «Новости»: the router's page for the feed (Telegram and the site's «Хроника», from our API). The stock launcher
///     has no such page; HfViews shows <c>HfNewsView</c> for it and <see cref="HfNewsModel" /> loads the posts.
/// </summary>
public sealed class HfNewsPageViewModel : PageViewModelBase
{
    public HfNewsPageViewModel(IScreen screen) : base(screen)
    {
    }
}
