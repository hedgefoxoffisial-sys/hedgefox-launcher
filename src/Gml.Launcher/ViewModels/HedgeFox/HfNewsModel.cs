using System;

namespace Gml.Launcher.ViewModels.HedgeFox;

/// <summary>«Новости»: the latest posts, newest first, as a list; the feed reloads now and then while the page shows.</summary>
public sealed class HfNewsModel : IHfScreenModel
{
    /// <summary>How many posts the page lists; the rest are on the site.</summary>
    public const int Limit = 12;

    public HfNewsModel(HfNewsPageViewModel page, HfServerInfoModel? info = null)
    {
        Page = page;
        Info = info ?? HfServerInfoModel.FromServices(Limit);
    }

    public HfNewsPageViewModel Page { get; }
    public HfServerInfoModel Info { get; }

    public IDisposable Start() => Info.Start();
}
