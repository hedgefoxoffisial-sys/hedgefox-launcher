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

/// <summary>The sign while the game downloads, and the one short line under it, in Russian.</summary>
public static class HfPlayProgress
{
    private static readonly Regex FilesLeft = new(@"^Осталось:? (\d+) файлов$", RegexOptions.Compiled);

    /// <summary>
    ///     The line under the sign: the detail («Осталось 120 файлов») when there is one, else the headline
    ///     («Обновление»). The stock line says «Осталось: N файлов» for every number; it is made to agree («1 файл»,
    ///     «2 файла») and loses the colon.
    /// </summary>
    public static string Step(string? headline, string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return headline?.Trim() ?? string.Empty;
        var text = description.Trim();
        var m = FilesLeft.Match(text);
        return m.Success && int.TryParse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var n)
            ? $"Осталось {n} {HfStrings.Plural(n, "файл", "файла", "файлов")}"
            : text;
    }

    public static string Percent(int? value) =>
        value is { } v ? Math.Clamp(v, 0, 100).ToString(CultureInfo.InvariantCulture) + " %" : string.Empty;

    /// <summary>The sign's label while it downloads: the percent, or «ЗАГРУЗКА…» while the size is unknown.</summary>
    public static string SignLabel(int? percent) => percent is null ? HfStrings.LoadingCaps : Percent(percent);
}

/// <summary>
///     Which version of the game (GML profile) «Играть» starts. Nobody has to choose: the launcher takes the one
///     advised for this PC. A pick in Настройки wins and is remembered; picking the advised version again means
///     «follow this PC» once more.
/// </summary>
public static class HfProfilePick
{
    /// <summary>Storage key of the player's own pick (a GML name), or null while the launcher picks.</summary>
    public const string StorageKey = "HfProfileChoice";

    /// <summary>
    ///     The version to select: the player's pick while it is still offered, else the advised one; null keeps the
    ///     current selection (no advice, or the advised version is not offered).
    /// </summary>
    public static ProfileReadDto? Pick(IEnumerable<ProfileReadDto> profiles, HfAdvice advice, string? choice)
    {
        var list = profiles.ToList();
        return Find(list, choice) ?? Find(list, advice.Recommended);
    }

    /// <summary>What to remember when the player picks <paramref name="picked" />: null for the advised version.</summary>
    public static string? Remember(string picked, HfAdvice advice) =>
        advice.Recommended is { } r && string.Equals(r, picked, StringComparison.OrdinalIgnoreCase) ? null : picked;

    private static ProfileReadDto? Find(List<ProfileReadDto> list, string? name) =>
        name is null ? null : list.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
///     The «рекомендуем» mark on the memory slider: the memory the chosen version asks for, which is also what the
///     game gets while memory is automatic (the stock «по профилю»). The thumb on the mark is automatic memory.
/// </summary>
public static class HfRamMark
{
    /// <summary>The version's memory (what automatic memory gives the game), or null when it does not say.</summary>
    public static int? ProfileMb(ProfileReadDto? profile) => profile is { RecommendedRam: > 0 and var mb } ? mb : null;

    /// <summary>The slider stands on the mark: the game follows the version's memory.</summary>
    public static bool IsOnMark(double value, int? markMb) => markMb is { } m && Math.Abs(value - m) < 1;

    /// <summary>Where <paramref name="value" /> sits between the slider's ends, 0…1.</summary>
    public static double Fraction(double value, double min, double max) =>
        max <= min ? 0 : Math.Clamp((value - min) / (max - min), 0, 1);
}
