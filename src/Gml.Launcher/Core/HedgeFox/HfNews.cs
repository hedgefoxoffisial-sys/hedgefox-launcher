using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace Gml.Launcher.Core.HedgeFox;

/// <summary>One post of GET /api/launcher/news: Telegram (source "tg") or the site's «Хроника» ("site").</summary>
public sealed record HfNewsItem(
    string Id,
    string Source,
    string Title,
    string Text,
    string Url,
    DateTimeOffset Date,
    string? ImageUrl)
{
    public bool IsTelegram => Source == "tg";
}

public sealed record HfNewsResult(IReadOnlyList<HfNewsItem> Items, bool Failed)
{
    public static readonly HfNewsResult Error = new([], true);
}

/// <summary>
///     Reads the news feed: a JSON array of posts. Posts without an id, a title, a date or an http(s) link are
///     skipped; text is plain and cut to 600 characters; newest first. Anything that is not an array is an error.
/// </summary>
public static class HfNewsParser
{
    public const int MaxText = 600;

    /// <summary>The posts newest first, at most <paramref name="limit" />; null when the body is not a feed.</summary>
    public static IReadOnlyList<HfNewsItem>? Parse(string? json, int limit = 20)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return null;
            return doc.RootElement.EnumerateArray()
                .Select(ReadItem)
                .OfType<HfNewsItem>()
                .OrderByDescending(i => i.Date)
                .Take(Math.Max(0, limit))
                .ToList();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static HfNewsItem? ReadItem(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Object)
            return null;
        var id = Str(e, "id");
        var title = Str(e, "title")?.Trim();
        var url = Str(e, "url");
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title) || !IsWebLink(url) ||
            !DateTimeOffset.TryParse(Str(e, "date"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal,
                out var date))
            return null;
        var image = Str(e, "image_url");
        return new HfNewsItem(id, Str(e, "source") ?? string.Empty, title, Cut(Str(e, "text")?.Trim() ?? string.Empty),
            url!, date, IsWebLink(image) ? image : null);
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static bool IsWebLink(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    private static string Cut(string text) => text.Length <= MaxText ? text : text[..(MaxText - 1)].TrimEnd() + "…";
}
