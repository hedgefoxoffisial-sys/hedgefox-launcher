using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Gml.Launcher.Core.HedgeFox;

/// <summary>A piece of a line with marked words: <see cref="Marked" /> pieces are drawn bright and bold.</summary>
public sealed record HfMarkedPart(string Text, bool Marked);

/// <summary>Lines with marked words: «Дальность *7 чанков*» marks «7 чанков». A star without a pair stays text.</summary>
public static class HfMarked
{
    public static IReadOnlyList<HfMarkedPart> Parse(string markup)
    {
        var parts = new List<HfMarkedPart>();
        var rest = markup;
        while (rest.Length > 0)
        {
            var open = rest.IndexOf('*');
            var close = open < 0 ? -1 : rest.IndexOf('*', open + 1);
            if (close < 0)
            {
                parts.Add(new HfMarkedPart(rest, false));
                break;
            }
            if (open > 0)
                parts.Add(new HfMarkedPart(rest[..open], false));
            if (close > open + 1)
                parts.Add(new HfMarkedPart(rest[(open + 1)..close], true));
            rest = rest[(close + 1)..];
        }
        return parts;
    }
}

/// <summary>
///     «Давай настроим лаунчер под тебя»: after the first login three columns, Слабый ПК / Обычный / Шейдеры, each with
///     the same place in the game seen in that version, a few lines with marked words and the advice for this PC.
/// </summary>
public static class HfWelcome
{
    /// <summary>Storage key: true once the player has seen the columns and chosen.</summary>
    public const string SeenKey = "HfWelcomeSeen";

    private const string Pictures = "avares://Gml.Launcher/Assets/HedgeFox/Images/Welcome/";
    private static readonly Regex Trademarks = new(@"\((R|TM|tm|r)\)|®|™", RegexOptions.Compiled);
    private static readonly Regex Spaces = new(@"\s{2,}", RegexOptions.Compiled);

    private static readonly string[] ColumnOrder = [ProfileCatalog.LowPc, ProfileCatalog.Normal, ProfileCatalog.Shaders];

    /// <summary>Слабый ПК, Обычный, Шейдеры, then any other version in the order given.</summary>
    public static IReadOnlyList<HfProfileCard> Order(IEnumerable<HfProfileCard> cards) =>
        cards.Select((card, i) => (card, i))
            .OrderBy(x => Rank(x.card.GmlName))
            .ThenBy(x => x.i)
            .Select(x => x.card)
            .ToList();

    /// <summary>The column's lines with marked words; the memory line only when <paramref name="ram" /> is known.</summary>
    public static IReadOnlyList<string> Points(string gmlName, string ram)
    {
        var memory = ram.Length > 0 ? $"Игре *{ram}* памяти" : null;
        string?[] lines = Key(gmlName) switch
        {
            0 => ["Дальность *7 чанков*", memory, "*Больше FPS*, проще графика", "Для *ноутбуков*"],
            1 => ["Дальность *12 чанков*", memory, "*Весь мир* как есть", "Для *большинства ПК*"],
            2 => ["*Живой свет* и вода", "Дальность *10 чанков*", memory, "Нужна *мощная видеокарта*"],
            _ => []
        };
        return lines.OfType<string>().ToList();
    }

    /// <summary>The same place in the game seen in this version; Обычный for versions the launcher does not know.</summary>
    public static string Picture(string gmlName) => Pictures + (Key(gmlName) switch
    {
        0 => "lowpc.jpg",
        2 => "shaders.jpg",
        _ => "normal.jpg"
    });

    /// <summary>«NVIDIA GeForce RTX 3060 · 16 ГБ памяти»: the best card and the memory; empty when nothing is known.</summary>
    public static string PcLine(HfHardware hardware)
    {
        var gpu = hardware.Gpus
            .Select(g => (g, rank: GpuRank(HardwareAdvisor.Classify(g))))
            .Where(x => x.rank >= 0)
            .OrderBy(x => x.rank)
            .Select(x => Clean(x.g.Name))
            .FirstOrDefault(n => n.Length > 0);
        var gb = (long)Math.Round(hardware.TotalRamMb / 1024.0, MidpointRounding.AwayFromZero);
        var memory = gb > 0 ? gb.ToString(CultureInfo.InvariantCulture) + " ГБ памяти" : null;
        return string.Join(" · ", new[] { gpu, memory }.OfType<string>());
    }

    private static int Rank(string gmlName) => Key(gmlName) is var k and >= 0 ? k : ColumnOrder.Length;

    private static int Key(string gmlName) =>
        Array.FindIndex(ColumnOrder, n => string.Equals(n, gmlName, StringComparison.OrdinalIgnoreCase));

    private static int GpuRank(HfGpuClass kind) => kind switch
    {
        HfGpuClass.DiscreteNvidiaAmd or HfGpuClass.AppleHigh => 0,
        HfGpuClass.DiscreteOther => 1,
        HfGpuClass.AppleBase or HfGpuClass.Integrated => 2,
        HfGpuClass.Unknown => 3,
        _ => -1
    };

    private static string Clean(string name) => Spaces.Replace(Trademarks.Replace(name, string.Empty), " ").Trim();
}
