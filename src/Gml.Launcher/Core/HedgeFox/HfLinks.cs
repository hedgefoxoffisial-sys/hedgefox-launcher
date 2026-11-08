using System;

namespace Gml.Launcher.Core.HedgeFox;

/// <summary>
///     Every address the launcher opens or reads, in one place. Pages open these in the browser
///     (PageViewModelBase.OpenLinkCommand); the launcher itself reads only <see cref="ApiBase" />.
///     Site paths end with a slash: without it the site answers with a redirect.
/// </summary>
public static class HfLinks
{
    /// <summary>Our site and API (Hono on the VPS). Change it here to point the launcher elsewhere.</summary>
    public const string ApiBase = "https://www.hedgefox.ru";

    public const string Site = "https://www.hedgefox.ru/";
    public const string Register = Site + "register/";
    public const string Forgot = Site + "forgot/";
    public const string Rules = Site + "rules/";
    public const string Wiki = Site + "wiki/";
    public const string Cabinet = Site + "cabinet/";
    public const string News = Site + "novosti/";
    public const string Faq = Site + "faq/";
    public const string Credits = Site + "credits/";
    public const string Download = Site + "download/";
    public const string InstallWindows = Download + "windows/";
    public const string InstallMacOs = Download + "macos/";
    public const string InstallLinux = Download + "linux/";

    /// <summary>The Telegram channel. Opened in the browser only: the launcher never reads t.me itself.</summary>
    public const string Telegram = "https://t.me/hedgefox_ru";

    public const string StatusApi = ApiBase + "/api/launcher/status";

    /// <summary>The install guide for the running OS: «Помощь» (SmartScreen, Gatekeeper, antivirus).</summary>
    public static string Install => InstallFor(OperatingSystem.IsWindows(), OperatingSystem.IsMacOS());

    public static string InstallFor(bool windows, bool macOs) =>
        windows ? InstallWindows : macOs ? InstallMacOs : InstallLinux;

    public static string NewsApi(int limit) => $"{ApiBase}/api/launcher/news?limit={limit}";

    /// <summary>The player's skin: the launcher's user uuid is the site uuid. A missing skin is an HTML 404.</summary>
    public static string Skin(string uuid) => $"{ApiBase}/skins/{Uri.EscapeDataString(uuid)}.png";
}
