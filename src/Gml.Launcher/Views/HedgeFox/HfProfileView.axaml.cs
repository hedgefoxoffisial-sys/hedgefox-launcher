using Avalonia.Markup.Xaml;
using Gml.Launcher.ViewModels.HedgeFox;
using Gml.Launcher.ViewModels.Pages;

namespace Gml.Launcher.Views.HedgeFox;

public partial class HfProfileView : HfPageView<ProfilePageViewModel, HfProfileModel>
{
    public HfProfileView()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override HfProfileModel CreateModel(ProfilePageViewModel page) => new(page);
}
