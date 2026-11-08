using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Reflection;
using Gml.Client.Interfaces;
using Gml.Launcher.Core.Services;
using Gml.Launcher.Models;
using Splat;

namespace Gml.Launcher.Core.HedgeFox;

/// <summary>
///     The HedgeFox services, registered by one line in ServiceLocator.RegisterServices (after the stock language
///     check). Screens get them from Splat: <c>Locator.Current.GetService&lt;IStatusService&gt;()</c>.
/// </summary>
public static class HedgeFoxServices
{
    public static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");

    /// <summary>
    ///     Russian always (spec «Настройки»: «по умолчанию русский, даже если язык ОС другой»). The HedgeFox settings
    ///     have no language picker, and v0.9 saved the OS language (en-US, zh-CN) as soon as Settings was opened, so a
    ///     saved language other than Russian is rewritten once; nothing is saved when nothing was.
    /// </summary>
    internal static void UseRussian(IStorageService storage)
    {
        var settings = storage.GetAsync<SettingsInfo>(StorageConstants.Settings).Result;
        if (settings is not null && settings.LanguageCode != Russian.Name)
            storage.SetAsync(StorageConstants.Settings, settings with { LanguageCode = Russian.Name }).Wait();

        // the stock resources and SettingsInfo.Default (which saves CurrentUICulture) both follow this
        Assets.Resources.Resources.Culture = Russian;
        CultureInfo.CurrentUICulture = Russian;
        CultureInfo.DefaultThreadCurrentUICulture = Russian;
    }

    public static void Register(IStorageService storage)
    {
        UseRussian(storage);

        var http = CreateHttpClient();
        var m = Locator.CurrentMutable;
        // sign-in failures in Russian on the form (the stock page would toast the raw exception text)
        if (Locator.Current.GetService<IGmlClientManager>() is { } gml)
            m.RegisterConstant(HfAuthGuard.Wrap(gml));
        m.RegisterConstant<IStatusService>(new StatusService(http));
        m.RegisterConstant<INewsService>(new NewsService(http));
        m.RegisterConstant<ISkinService>(new SkinService(http));
        m.RegisterConstant<IHardwareProbe>(new HardwareProbe());
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            AutomaticDecompression = DecompressionMethods.All
        };
        var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0";
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"HedgeFoxLauncher/{version}");
        return http;
    }
}
