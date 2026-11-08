using System;
using System.Collections.Generic;
using System.Globalization;

namespace Gml.Launcher.Core.HedgeFox;

/// <summary>
///     Russian UI text of the HedgeFox screens, in one place: short, «ты», in the site's voice
///     (web/site/brand/README.md, lib/messages.ts). No dates of the opening, no countdowns.
///     Tiny5 labels are written in capitals here, because Avalonia has no text-transform.
/// </summary>
public static class HfStrings
{
    // ----- brand -----
    public const string Name = "HedgeFox";
    public const string Tagline = "Пираты над Пеленой";
    public const string TaglinePixel = "ПИРАТЫ НАД ПЕЛЕНОЙ";

    // ----- status line -----
    public const string StatusLoading = "Проверяем…";
    public const string StatusNoData = "Нет данных";
    public const string StatusOffline = "Сервер спит";

    // ----- Pelena -----
    public const string PelenaKicker = "ПЕЛЕНА";
    public const string PelenaSleeps = "Пелена спит";
    public const string PelenaWaking = "Пелена просыпается";
    public const string PelenaRising = "поднимается";
    public const string PelenaFalling = "опускается";

    // ----- sign-in -----
    public const string LoginField = "Ник или почта";
    public const string PasswordField = "Пароль";
    public const string Register = "Регистрация";
    public const string Forgot = "Забыл пароль?";
    public const string SignInCaps = "ВОЙТИ";
    public const string TwoFactorField = "Код из приложения";
    public const string TwoFactorConfirmCaps = "ПОДТВЕРДИТЬ";

    // ----- main screen -----
    public const string Profile = "Профиль";
    public const string Settings = "Настройки";
    public const string Play = "Играть";
    public const string PlayCaps = "ИГРАТЬ";
    public const string LoadingCaps = "ЗАГРУЗКА…";
    public const string Reinstall = "Переустановить";
    public const string ReinstallHint = "Если игра не запускается, скачаем её файлы заново.";
    public const string ReinstallAskTitle = "Переустановить игру?";
    public const string ReinstallAsk =
        "Удалим папку этого профиля: настройки игры, скриншоты, ресурспаки и шейдеры — и скачаем всё заново.";
    public const string ReinstallYes = "Да, переустановить";
    public const string Cancel = "Отмена";

    // ----- news -----
    public const string NewsEmpty = "Новостей пока нет. Летопись — на сайте.";
    public const string NewsFailed = "Новости не загрузились. Попробуем позже.";

    // ----- footer -----
    public const string Site = "Сайт";
    public const string Telegram = "Telegram";
    public const string Help = "Помощь";

    // ----- profile page -----
    public const string ChangeSkin = "Сменить скин на сайте";
    public const string LogoutAccount = "Выйти из аккаунта";
    public const string NoSkin = "Скина пока нет — загрузи его в кабинете на сайте.";

    // ----- settings -----
    public const string SettingsCaps = "НАСТРОЙКИ";
    public const string GameVersion = "Версия игры";
    public const string MemorySetting = "Память";
    public const string MemoryRecommendedMark = "рекомендуем";
    public const string WindowSetting = "Окно игры";
    public const string FullScreen = "Полный экран";
    public const string Mods = "Моды";
    public const string ModsNote = "Сработает при следующем запуске.";
    public const string More = "Дополнительно";
    public const string WindowSize = "Размер окна";
    public const string WindowWidth = "Ширина";
    public const string WindowHeight = "Высота";
    public const string GameFolder = "Папка игры";
    public const string FolderHasSpaces = "В пути есть пробелы. Выбери папку без них.";
    public const string FolderFailed = "Не получилось сменить папку. Попробуй другую.";
    public const string ChangeFolder = "Выбрать папку";
    public const string OpenFolder = "Открыть";
    public const string Back = "Назад";

    // ----- window -----
    public const string Minimize = "Свернуть";
    public const string Close = "Закрыть";

    // ----- errors (lib/messages.ts) -----
    public const string NoConnection = "Нет связи с сервером. Проверь интернет и попробуй ещё раз.";
    public const string TooSlow = "Сервер долго не отвечает. Попробуй ещё раз.";
    public const string WrongLogin = "Неверный ник, почта или пароль.";
    public const string FillLogin = "Введи ник или почту и пароль.";
    public const string TwoFactorWrong = "Код не подошёл. Попробуй ещё раз.";
    public const string LoginOffline = "Сервер входа не отвечает, поэтому войти сейчас нельзя. Перезапусти лаунчер чуть позже.";

    /// <summary>Tiny5 hints under the splash meter, changing every 1.6 s and stopping at the last (as on the site).</summary>
    public static readonly IReadOnlyList<string> SplashHints =
        ["ЗАВОДИМ ШЕСТЕРЁНКИ…", "ПОДНИМАЕМ ДИРИЖАБЛЬ…", "ПОЧТИ ГОТОВО…"];

    public static readonly TimeSpan SplashHintEvery = TimeSpan.FromSeconds(1.6);

    public const string SplashSlow = "СЕРВЕР ВХОДА ДОЛГО ОТВЕЧАЕТ…";
    public const string SplashOffline = "СЕРВЕР ВХОДА НЕ ОТВЕЧАЕТ";

    /// <summary>
    ///     The line under the splash meter: the hints, unless the stock checks report the sign-in server slow
    ///     (<paramref name="slowStatus" />) or down (<paramref name="offlineStatus" />), their own localized texts.
    /// </summary>
    public static string SplashLine(string? status, TimeSpan elapsed, string slowStatus, string offlineStatus) =>
        status == offlineStatus ? SplashOffline
        : status == slowStatus ? SplashSlow
        : SplashHintAt(elapsed);

    /// <summary>The hint shown <paramref name="elapsed" /> after the splash appeared.</summary>
    public static string SplashHintAt(TimeSpan elapsed) =>
        SplashHints[(int)Math.Min(Math.Max(0, elapsed.Ticks) / SplashHintEvery.Ticks, SplashHints.Count - 1)];

    private static readonly string[] MonthsGenitive =
    [
        "января", "февраля", "марта", "апреля", "мая", "июня",
        "июля", "августа", "сентября", "октября", "ноября", "декабря"
    ];

    public static string StatusOnline(int players, int max) => $"Онлайн {players} из {max}";

    /// <summary>The status in full: «Онлайн N из M», «Сервер спит», «Нет данных» or «Проверяем…».</summary>
    public static string StatusLine(HfStatusSnapshot status) => status.Kind switch
    {
        HfStatusKind.Online => StatusOnline(status.Players ?? 0, status.Max ?? 0),
        HfStatusKind.Offline => StatusOffline,
        HfStatusKind.Loading => StatusLoading,
        _ => StatusNoData
    };

    /// <summary>
    ///     The one small status line at the bottom of the sign-in and main screens: «Онлайн 7 из 45» or «Сервер спит»;
    ///     null while there is nothing to say (no answer yet, or no data): the line is then hidden, never a made-up «0».
    /// </summary>
    public static string? StatusShort(HfStatusSnapshot status) => status.Kind switch
    {
        HfStatusKind.Online => StatusOnline(status.Players ?? 0, status.Max ?? 0),
        HfStatusKind.Offline => StatusOffline,
        _ => null
    };

    /// <summary>«Пелена: Y 80»: whole blocks, a real minus sign below zero.</summary>
    public static string PelenaLevel(double y)
    {
        var v = (int)Math.Round(y, MidpointRounding.AwayFromZero);
        return "Пелена: Y " + (v < 0 ? "−" + (-v).ToString(CultureInfo.InvariantCulture) : v.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>«сегодня», «вчера», «8 ноября», «31 декабря 2025», in the time zone of <paramref name="now" />.</summary>
    public static string NewsDate(DateTimeOffset date, DateTimeOffset now)
    {
        var day = date.ToOffset(now.Offset).Date;
        var today = now.Date;
        if (day == today)
            return "сегодня";
        if (day == today.AddDays(-1))
            return "вчера";
        var text = $"{day.Day} {MonthsGenitive[day.Month - 1]}";
        return day.Year == today.Year ? text : $"{text} {day.Year}";
    }

    public static string NewsSource(string source) => source switch
    {
        "tg" => Telegram,
        "site" => Site,
        _ => "Новости"
    };

    /// <summary>Russian plural: 1 игрок, 3 игрока, 5 игроков, 21 игрок.</summary>
    public static string Plural(int n, string one, string few, string many)
    {
        var abs = Math.Abs(n);
        var mod100 = abs % 100;
        var mod10 = abs % 10;
        if (mod100 is >= 11 and <= 14)
            return many;
        return mod10 switch
        {
            1 => one,
            >= 2 and <= 4 => few,
            _ => many
        };
    }
}
