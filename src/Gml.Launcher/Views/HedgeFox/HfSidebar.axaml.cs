using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Gml.Launcher.Views.HedgeFox;

/// <summary>The sidebar once signed in; DataContext: <see cref="ViewModels.HedgeFox.HfShellModel" />.</summary>
public partial class HfSidebar : UserControl
{
    public HfSidebar()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
