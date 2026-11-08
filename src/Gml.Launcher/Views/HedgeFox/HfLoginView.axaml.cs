using System;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Gml.Launcher.ViewModels.HedgeFox;
using Gml.Launcher.ViewModels.Pages;
using ReactiveUI;

namespace Gml.Launcher.Views.HedgeFox;

public partial class HfLoginView : HfPageView<LoginPageViewModel, HfLoginModel>
{
    private IDisposable? _focus;

    public HfLoginView()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override HfLoginModel CreateModel(LoginPageViewModel page) => new(page);

    // the cursor waits in the nick field, and in the code field once the 2FA step opens
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (ViewModel is not { } page)
            return;
        _focus = page.WhenAnyValue(p => p.Is2FaVisible).Subscribe(twoFactor =>
            Dispatcher.UIThread.Post(() => this.Get<TextBox>(twoFactor ? "CodeBox" : "LoginBox").Focus(),
                DispatcherPriority.Loaded));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _focus?.Dispose();
        _focus = null;
    }
}
