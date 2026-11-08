using Avalonia.Markup.Xaml;
using Gml.Launcher.ViewModels.HedgeFox;
using Gml.Launcher.ViewModels.Pages;

namespace Gml.Launcher.Views.HedgeFox;

public partial class HfOverviewView : HfPageView<OverviewPageViewModel, HfOverviewModel>
{
    public HfOverviewView()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override HfOverviewModel CreateModel(OverviewPageViewModel page) => new(page);
}
