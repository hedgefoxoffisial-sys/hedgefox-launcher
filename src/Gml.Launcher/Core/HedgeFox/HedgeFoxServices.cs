using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Reflection;
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

    /// <summary>Russian unless the player picked a language in the settings.</summary>
    public static CultureInfo? DefaultUiCulture(string? savedLanguageCode) =>
        string.IsNullOrWhiteSpace(savedLanguageCode) ? Russian : null;

    public static void Register(IStorageService storage)
    {
        var saved = storage.GetAsync<SettingsInfo>(StorageConstants.Settings).Result?.LanguageCode;
        if (DefaultUiCulture(saved) is { } culture)
        {
            // the stock resources and SettingsInfo.Default (which saves CurrentUICulture) both follow this
            Assets.Resources.Resources.Culture = culture;
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
        }

        var http = CreateHttpClient();
        var m = Locator.CurrentMutable;
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
