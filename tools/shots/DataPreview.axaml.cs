using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;

namespace HedgeFox.Shots;

public partial class DataPreview : ReactiveUserControl<DataPreviewViewModel>
{
    public DataPreview()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
