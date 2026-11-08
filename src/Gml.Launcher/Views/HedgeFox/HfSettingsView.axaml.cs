using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using GamerVII.Notification.Avalonia;
using Gml.Launcher.Assets;
using Gml.Launcher.Core;
using Gml.Launcher.Core.HedgeFox;
using Gml.Launcher.ViewModels.HedgeFox;
using Gml.Launcher.ViewModels.Pages;
using Sentry;

namespace Gml.Launcher.Views.HedgeFox;

public partial class HfSettingsView : HfPageView<SettingsPageViewModel, HfSettingsModel>
{
    public HfSettingsView()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override HfSettingsModel CreateModel(SettingsPageViewModel page) => new(page);

    // the same checks as the stock settings page: one folder, no spaces in the path
    private async void OnPickFolder(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } page || TopLevel.GetTopLevel(this) is not { } top)
            return;
        try
        {
            var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                AllowMultiple = false,
                Title = HfStrings.GameFolder
            });
            if (folders.Count != 1)
                return;
            var path = folders[0].Path.LocalPath;
            if (path.Any(char.IsWhiteSpace))
                throw new ArgumentException("The game folder path has spaces");
            page.InstallationFolder = Path.GetFullPath(path);
            page.ChangeFolder();
        }
        catch (Exception exception)
        {
            SentrySdk.CaptureException(exception);
            ShowFolderError(page);
        }
    }

    private void OnOpenFolder(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not { InstallationFolder: { Length: > 0 } folder } page)
            return;
        try
        {
            Directory.CreateDirectory(folder);
            var opener = OperatingSystem.IsWindows() ? "explorer.exe" : OperatingSystem.IsMacOS() ? "open" : "xdg-open";
            Process.Start(opener, folder);
        }
        catch (Exception exception)
        {
            SentrySdk.CaptureException(exception);
            ShowFolderError(page);
        }
    }

    private static void ShowFolderError(SettingsPageViewModel page) =>
        page.MainViewModel.Manager
            .CreateMessage(true, "#FF6B7A",
                page.LocalizationService.GetString(SystemConstants.Error),
                page.LocalizationService.GetString(SystemConstants.InvalidFolder))
            .Dismiss()
            .WithDelay(TimeSpan.FromSeconds(3))
            .Queue();
}
