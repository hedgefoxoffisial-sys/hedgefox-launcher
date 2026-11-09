using Avalonia.Markup.Xaml;
using Gml.Launcher.ViewModels.HedgeFox;

namespace Gml.Launcher.Views.HedgeFox;

public partial class HfNewsView : HfPageView<HfNewsPageViewModel, HfNewsModel>
{
    public HfNewsView()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override HfNewsModel CreateModel(HfNewsPageViewModel page) => new(page);
}
