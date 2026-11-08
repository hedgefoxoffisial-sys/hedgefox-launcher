using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Gml.Dto.Profile;

namespace Gml.Launcher.Core.HedgeFox;

/// <summary>One news card: source label (Tiny5 capitals), Russian date, title, plain text, the link it opens.</summary>
public sealed record HfNewsCard(string Id, string Source, string Date, string Title, string Text, string Url, bool IsTelegram)
{
    private static readonly Regex BlankLines = new(@"\n[ \t]*(\n[ \t]*)+", RegexOptions.Compiled);

    public static HfNewsCard From(HfNewsItem item, DateTimeOffset now) =>
        new(item.Id,
            HfStrings.NewsSource(item.Source).ToUpper(CultureInfo.GetCultureInfo("ru-RU")),
            HfStrings.NewsDate(item.Date, now),
            item.Title,
            BlankLines.Replace(item.Text.Replace("\r\n", "\n"), "\n").Trim(),
            item.Url,
            item.IsTelegram);

    /// <summary>What the feed says instead of cards: null when there are posts.</summary>
    public static string? Message(HfNewsResult result) =>
        result.Failed ? HfStrings.NewsFailed : result.Items.Count == 0 ? HfStrings.NewsEmpty : null;
}

/// <param name="Profile">The GML profile the card selects.</param>
/// <param name="Ram">Memory the profile gives the game, «6 ГБ».</param>
/// <param name="Recommended">«Советуем для твоего ПК» (one card at most).</param>
/// <param name="ShadersAlsoFit">On «Шейдеры» when this PC can run them too.</param>
/// <param name="Selected">The player's choice (it wins over the advice).</param>
public sealed record HfProfileCard(
    ProfileReadDto Profile,
    string GmlName,
    string Title,
    string Hint,
    string Ram,
    bool Recommended,
    bool ShadersAlsoFit,
    bool Selected)
{
    public bool HasHint => Hint.Length > 0;
    public bool HasRam => Ram.Length > 0;
}

public static class HfProfileCards
{
    /// <summary>Cards in the catalog's order: titles and hints from <see cref="ProfileCatalog" />, the advice on one.</summary>
    public static IReadOnlyList<HfProfileCard> Build(IEnumerable<ProfileReadDto> profiles, HfAdvice advice, string? selected) =>
        ProfileCatalog.Order(profiles, p => p.Name).Select(p =>
        {
            var info = ProfileCatalog.For(p.Name, p.DisplayName, p.RecommendedRam);
            return new HfProfileCard(p, p.Name, info.Title, info.Hint, ProfileCatalog.RamLabel(info.RamMb),
                advice.Recommended is { } r && string.Equals(r, p.Name, StringComparison.OrdinalIgnoreCase),
                advice.ShadersOk && string.Equals(p.Name, ProfileCatalog.Shaders, StringComparison.OrdinalIgnoreCase),
                selected is not null && string.Equals(selected, p.Name, StringComparison.Ordinal));
        }).ToList();
}

/// <summary>The line under «ИГРАТЬ» while the game downloads: step, percent and speed, in Russian.</summary>
public static class HfPlayProgress
{
    private static readonly Regex SpeedPattern = new(@"^\s*([\d.,]+)\s*(B|KB|MB|GB|TB)/s\s*$", RegexOptions.Compiled);

    private static readonly Dictionary<string, string> Units = new()
    {
        ["B"] = "Б", ["KB"] = "КБ", ["MB"] = "МБ", ["GB"] = "ГБ", ["TB"] = "ТБ"
    };

    private static readonly Regex FilesLeft = new(@"^Осталось: (\d+) файлов$", RegexOptions.Compiled);

    /// <summary>
    ///     The detail («Осталось: 120 файлов») when there is one, else the headline («Обновление»). The stock line
    ///     says «файлов» for every number; it is made to agree («1 файл», «2 файла»).
    /// </summary>
    public static string Step(string? headline, string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return headline?.Trim() ?? string.Empty;
        var text = description.Trim();
        var m = FilesLeft.Match(text);
        return m.Success && int.TryParse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var n)
            ? $"Осталось: {n} {HfStrings.Plural(n, "файл", "файла", "файлов")}"
            : text;
    }

    public static string Percent(int? value) =>
        value is { } v ? Math.Clamp(v, 0, 100).ToString(CultureInfo.InvariantCulture) + " %" : string.Empty;

    /// <summary>The sign's label while it downloads: the percent, or «ЗАГРУЗКА…» while the size is unknown.</summary>
    public static string SignLabel(int? percent) => percent is null ? HfStrings.LoadingCaps : Percent(percent);

    /// <summary>The one line under the sign: the step and, while bytes arrive, the speed.</summary>
    public static string Line(string? headline, string? description, string? speed)
    {
        var step = Step(headline, description);
        var rate = Speed(speed);
        return step.Length == 0 ? rate : rate.Length == 0 ? step : step + HfStrings.Dot + rate;
    }

    /// <summary>The stock counter's «3.5 MB/s» as «3,5 МБ/с».</summary>
    public static string Speed(string? speed)
    {
        if (string.IsNullOrWhiteSpace(speed))
            return string.Empty;
        var m = SpeedPattern.Match(speed);
        return m.Success ? $"{m.Groups[1].Value.Replace('.', ',')} {Units[m.Groups[2].Value]}/с" : speed.Trim();
    }
}

/// <summary>The «советуем» mark on the memory slider: the memory of the profile the advice picks.</summary>
public static class HfRamMark
{
    /// <summary>Memory of the advised profile, or null without advice or when this PC has less than that.</summary>
    public static int? RecommendedMb(HfAdvice advice, ulong maxRamMb)
    {
        if (advice.Recommended is not { } name)
            return null;
        var mb = ProfileCatalog.For(name, null).RamMb;
        return mb > 0 && (ulong)mb <= maxRamMb ? mb : null;
    }

    /// <summary>Where <paramref name="value" /> sits between the slider's ends, 0…1.</summary>
    public static double Fraction(double value, double min, double max) =>
        max <= min ? 0 : Math.Clamp((value - min) / (max - min), 0, 1);
}
