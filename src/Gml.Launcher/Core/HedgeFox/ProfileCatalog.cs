using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Gml.Launcher.Core.HedgeFox;

/// <param name="GmlName">The profile's name in GML (the key).</param>
/// <param name="Title">Card title: «Обычный», «Слабый ПК», «Шейдеры», or GML's displayName for others.</param>
/// <param name="Hint">One line under the title; empty for profiles the launcher does not know.</param>
/// <param name="RamMb">Memory the profile gives the game.</param>
public sealed record HfProfileInfo(string GmlName, string Title, string Hint, int RamMb, bool Known);

/// <summary>Profile cards: the spec's table, in one place. HedgeFox-Creator never reaches the list (it is off).</summary>
public static class ProfileCatalog
{
    public const string Normal = "HedgeFox";
    public const string LowPc = "HedgeFox-LowPC";
    public const string Shaders = "HedgeFox-Shaders";

    public static IReadOnlyList<HfProfileInfo> Known { get; } =
    [
        new(Normal, "Обычный", "Полная дальность и все корабли. Для большинства ПК.", 6144, true),
        new(LowPc, "Слабый ПК", "Короче дальность, легче графика. Для ноутбуков и ПК до 8 ГБ.", 4096, true),
        new(Shaders, "Шейдеры", "Красиво: свет и тени. Нужна хорошая видеокарта.", 8192, true)
    ];

    /// <summary>The card for a GML profile; unknown profiles show GML's displayName and its recommended memory.</summary>
    public static HfProfileInfo For(string gmlName, string? displayName, int recommendedRamMb = 0)
    {
        var known = Known.FirstOrDefault(p => string.Equals(p.GmlName, gmlName, StringComparison.OrdinalIgnoreCase));
        if (known is not null)
            return known;
        var title = string.IsNullOrWhiteSpace(displayName) ? gmlName : displayName.Trim();
        return new HfProfileInfo(gmlName, title, string.Empty, recommendedRamMb, false);
    }

    /// <summary>Cards in the table's order (Обычный, Слабый ПК, Шейдеры), unknown profiles after them.</summary>
    public static IReadOnlyList<T> Order<T>(IEnumerable<T> profiles, Func<T, string> gmlName) =>
        profiles.Select((p, i) => (p, i))
            .OrderBy(x => IndexOf(gmlName(x.p)))
            .ThenBy(x => x.i)
            .Select(x => x.p)
            .ToList();

    private static int IndexOf(string gmlName)
    {
        for (var i = 0; i < Known.Count; i++)
            if (string.Equals(Known[i].GmlName, gmlName, StringComparison.OrdinalIgnoreCase))
                return i;
        return int.MaxValue;
    }

    /// <summary>«6 ГБ», «1,5 ГБ»; empty when unknown.</summary>
    public static string RamLabel(int mb)
    {
        if (mb <= 0)
            return string.Empty;
        var gb = Math.Round(mb / 1024.0, 1);
        return gb.ToString(gb % 1 == 0 ? "0" : "0.0", CultureInfo.GetCultureInfo("ru-RU")) + " ГБ";
    }
}
