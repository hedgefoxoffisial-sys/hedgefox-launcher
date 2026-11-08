using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Gml.Launcher.Core.HedgeFox;

/// <param name="Png">A 64×64 or 64×32 Minecraft skin.</param>
/// <param name="IsFallback">The bundled silhouette: the player has no skin on the site (or it did not load).</param>
public sealed record HfSkin(byte[] Png, bool IsFallback);

public interface ISkinService
{
    /// <summary>The player's skin by site uuid (= the launcher user's uuid). Never throws.</summary>
    Task<HfSkin> GetAsync(string uuid, CancellationToken cancellationToken = default);
}

/// <summary>
///     https://www.hedgefox.ru/skins/{uuid}.png, nginx static on our VPS. A missing skin is the site's HTML 404,
///     so anything that is not a PNG skin becomes the bundled silhouette (<see cref="HfSkinImages.Silhouette" />).
/// </summary>
public sealed class SkinService : ISkinService
{
    private static byte[]? _silhouette;

    private readonly HttpClient _http;
    private readonly TimeSpan _timeout;
    private readonly Func<byte[]> _fallback;

    public SkinService(HttpClient http, TimeSpan? timeout = null, Func<byte[]>? fallback = null)
    {
        _http = http;
        _timeout = timeout ?? StatusService.DefaultTimeout;
        _fallback = fallback ?? BundledSilhouette;
    }

    /// <summary>The «no skin» silhouette, drawn in the site's violets (no file: tests and the app share it).</summary>
    public static byte[] BundledSilhouette() => _silhouette ??= HfSkinImages.Silhouette();

    public async Task<HfSkin> GetAsync(string uuid, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(uuid))
            return Fallback();
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_timeout);
            using var response = await _http.GetAsync(HfLinks.Skin(uuid.Trim()), cts.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return Fallback();
            var bytes = await response.Content.ReadAsByteArrayAsync(cts.Token).ConfigureAwait(false);
            return HfSkinImages.IsSkinPng(bytes) ? new HfSkin(bytes, false) : Fallback();
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return Fallback();
        }
    }

    private HfSkin Fallback() => new(_fallback(), true);
}

/// <summary>Pictures cut from a skin, scaled with nearest-neighbour (pixel art never blurs).</summary>
public static class HfSkinImages
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>A PNG whose header says 64×64 or 64×32 (or an HD multiple of those).</summary>
    public static bool IsSkinPng(byte[] bytes)
    {
        if (bytes.Length < 24 || !bytes.AsSpan(0, 8).SequenceEqual(PngSignature))
            return false;
        var width = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
        var height = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
        return width >= 64 && width % 64 == 0 && (height == width || height * 2 == width);
    }

    /// <summary>The head's front (8×8) with the hat layer on top, as a PNG of 8·<paramref name="scale" /> px.</summary>
    public static byte[] Face(byte[] skinPng, int scale)
    {
        using var skin = Image.Load<Rgba32>(skinPng);
        var f = Math.Max(1, skin.Width / 64);
        using var face = new Image<Rgba32>(8 * f, 8 * f);
        for (var y = 0; y < 8 * f; y++)
        for (var x = 0; x < 8 * f; x++)
        {
            var bottom = skin[8 * f + x, 8 * f + y];
            var top = skin[40 * f + x, 8 * f + y];
            face[x, y] = Over(top, bottom);
        }

        face.Mutate(c => c.Resize(8 * scale, 8 * scale, KnownResamplers.NearestNeighbor));
        using var ms = new MemoryStream();
        face.SaveAsPng(ms);
        return ms.ToArray();
    }

    // 64×64 skin layout: (x, y, w, h) of each part's top and front; every other face gets the side colour
    private static readonly (int X, int Y, int W, int H, bool Front)[] Fronts =
    [
        (8, 0, 8, 8, false), (8, 8, 8, 8, true), // head
        (20, 16, 8, 4, false), (20, 20, 8, 12, true), // body
        (44, 16, 4, 4, false), (44, 20, 4, 12, true), // right arm
        (4, 16, 4, 4, false), (4, 20, 4, 12, true), // right leg
        (20, 48, 4, 4, false), (20, 52, 4, 12, true), // left leg
        (36, 48, 4, 4, false), (36, 52, 4, 12, true) // left arm
    ];

    private static readonly (int X, int Y, int W, int H)[] Blocks = [(0, 0, 32, 16), (0, 16, 56, 16), (16, 48, 32, 16)];

    /// <summary>A plain figure: line-violet front, line-strong top, raised-hover sides (web/site :root colours).</summary>
    public static byte[] Silhouette()
    {
        var side = new Rgba32(0x46, 0x30, 0x7D, 255);
        var front = new Rgba32(0x5F, 0x42, 0x85, 255);
        var top = new Rgba32(0x79, 0x56, 0xB7, 255);
        using var image = new Image<Rgba32>(64, 64);
        foreach (var b in Blocks)
            Fill(image, b.X, b.Y, b.W, b.H, side);
        // the unused corners of the head and limb rows stay transparent, like in a real skin
        foreach (var (x, y, w, h) in new[]
                 {
                     (0, 0, 8, 8), (24, 0, 8, 8), (0, 16, 4, 4), (12, 16, 8, 4), (36, 16, 8, 4), (52, 16, 4, 4),
                     (16, 48, 4, 4), (28, 48, 8, 4), (44, 48, 4, 4)
                 })
            Fill(image, x, y, w, h, default);
        foreach (var f in Fronts)
            Fill(image, f.X, f.Y, f.W, f.H, f.Front ? front : top);
        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    private static void Fill(Image<Rgba32> image, int x, int y, int w, int h, Rgba32 color)
    {
        for (var j = y; j < y + h; j++)
        for (var i = x; i < x + w; i++)
            image[i, j] = color;
    }

    private static Rgba32 Over(Rgba32 top, Rgba32 bottom)
    {
        if (top.A == 255)
            return top;
        if (top.A == 0)
            return new Rgba32(bottom.R, bottom.G, bottom.B, 255);
        var a = top.A / 255f;
        return new Rgba32(
            (byte)Math.Round(top.R * a + bottom.R * (1 - a)),
            (byte)Math.Round(top.G * a + bottom.G * (1 - a)),
            (byte)Math.Round(top.B * a + bottom.B * (1 - a)),
            255);
    }
}
