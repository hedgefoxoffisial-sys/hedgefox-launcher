using System;
using System.Globalization;
using System.Text.Json;

namespace Gml.Launcher.Core.HedgeFox;

/// <summary>The Pelena's level from `hf fog`: current Y and where it is heading. No schedule (no dates yet).</summary>
public sealed record HfPelena(double Y, double Target);

public enum HfStatusKind
{
    /// <summary>Before the first answer.</summary>
    Loading,

    /// <summary>Our API did not answer or answered nonsense: «Нет данных», never a fake 0.</summary>
    NoData,

    /// <summary>The API answered: the game server is down. «Сервер спит».</summary>
    Offline,

    /// <summary>The game server is up: players of max.</summary>
    Online
}

/// <summary>GET /api/launcher/status, as the launcher understands it.</summary>
public sealed record HfStatusSnapshot(
    HfStatusKind Kind,
    int? Players = null,
    int? Max = null,
    string? Version = null,
    DateTimeOffset? CheckedAt = null,
    HfPelena? Pelena = null)
{
    public static readonly HfStatusSnapshot Loading = new(HfStatusKind.Loading);
    public static readonly HfStatusSnapshot NoData = new(HfStatusKind.NoData);
}

/// <summary>
///     Reads the status JSON: <c>{ online, players, max, version, checked_at, pelena: { y, target } | null }</c>.
///     Anything that is not that shape (error objects, HTML from a proxy, an online server without numbers) is
///     NoData, so the window never shows a made-up «0». A broken pelena drops only the pelena.
/// </summary>
public static class HfStatusParser
{
    public static HfStatusSnapshot Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return HfStatusSnapshot.NoData;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("online", out var online) ||
                online.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                return HfStatusSnapshot.NoData;

            var checkedAt = ReadDate(root, "checked_at");
            if (!online.GetBoolean())
                return new HfStatusSnapshot(HfStatusKind.Offline, CheckedAt: checkedAt);

            if (!TryInt(root, "players", out var players) || players < 0 || !TryInt(root, "max", out var max) || max <= 0)
                return HfStatusSnapshot.NoData;

            return new HfStatusSnapshot(HfStatusKind.Online, players, max, ReadString(root, "version"), checkedAt,
                ReadPelena(root));
        }
        catch (JsonException)
        {
            return HfStatusSnapshot.NoData;
        }
    }

    private static bool TryInt(JsonElement root, string name, out int value)
    {
        value = 0;
        return root.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out value);
    }

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null;

    private static DateTimeOffset? ReadDate(JsonElement root, string name) =>
        ReadString(root, name) is { } s && DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal, out var date)
            ? date
            : null;

    private static HfPelena? ReadPelena(JsonElement root)
    {
        if (!root.TryGetProperty("pelena", out var p) || p.ValueKind != JsonValueKind.Object)
            return null;
        if (!p.TryGetProperty("y", out var y) || y.ValueKind != JsonValueKind.Number ||
            !p.TryGetProperty("target", out var target) || target.ValueKind != JsonValueKind.Number)
            return null;
        return new HfPelena(y.GetDouble(), target.GetDouble());
    }
}

/// <summary>
///     The Pelena widget's wording (spec «Пелена на виджете»): y ≤ −64 → «Пелена спит» («Пелена просыпается» when its
///     target is higher); otherwise «Пелена: Y {y}» and a bar from −64 to 200; target ≠ y → «поднимается» or
///     «опускается». Never a date.
/// </summary>
public sealed record HfPelenaView(bool Sleeping, string Title, bool ShowBar, double Fraction, string? Trend)
{
    public const double Bottom = -64;
    public const double Top = 200;

    /// <summary>For the status line: «Пелена спит», «Пелена поднимается», «Пелена: Y 80».</summary>
    public string Short => Trend is { } trend ? "Пелена " + trend : Title;

    /// <summary>Changes smaller than this are noise in the 1-decimal value `hf fog` prints.</summary>
    private const double Still = 0.05;

    public static HfPelenaView? From(HfPelena? pelena)
    {
        if (pelena is null)
            return null;
        var sleeping = pelena.Y <= Bottom;
        var trend = pelena.Target > pelena.Y + Still ? HfStrings.PelenaRising
            : pelena.Target < pelena.Y - Still ? HfStrings.PelenaFalling
            : null;
        // asleep it has no trend: with a target above the bottom it «просыпается» (never «спит» next to «поднимается»)
        return sleeping
            ? new HfPelenaView(true, pelena.Target > Bottom + Still ? HfStrings.PelenaWaking : HfStrings.PelenaSleeps,
                false, 0, null)
            : new HfPelenaView(false, HfStrings.PelenaLevel(pelena.Y), true,
                Math.Clamp((pelena.Y - Bottom) / (Top - Bottom), 0, 1), trend);
    }
}
