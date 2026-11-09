using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace Gml.Launcher.Views.HedgeFox.Controls;

/// <summary>
///     8×8 pixel icons in the style of the site (web/site/src/lib/pixel-icons.ts): one square per pixel, filled with
///     <see cref="Foreground" />. Crisp at 8, 16, 24 and 32 px. The site's fonts have no ✓ → ▶: use these instead.
/// </summary>
public class PixelIcon : Control
{
    public static readonly StyledProperty<string?> KindProperty =
        AvaloniaProperty.Register<PixelIcon, string?>(nameof(Kind));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<PixelIcon>();

    /// <summary>Bitmaps: the site's set (check … cross) plus launcher icons (minimize … globe).</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Bitmaps = new Dictionary<string, string[]>
    {
        ["check"] = ["........", ".......#", "......##", "#....##.", "##..##..", ".####...", "..##....", "........"],
        ["warn"] = ["...##...", "...##...", "...##...", "...##...", "...##...", "........", "...##...", "...##..."],
        ["info"] = ["...##...", "...##...", "........", "..###...", "...##...", "...##...", "...##...", "..####.."],
        ["error"] = ["##....##", "###..###", ".######.", "..####..", "..####..", ".######.", "###..###", "##....##"],
        ["play"] = [".#......", ".##.....", ".###....", ".####...", ".####...", ".###....", ".##.....", ".#......"],
        ["burger"] = ["........", "#######.", "........", "#######.", "........", "#######.", "........", "........"],
        ["dot"] = ["........", "........", "..####..", "..####..", "..####..", "..####..", "........", "........"],
        ["mail"] = ["........", "########", "##....##", "#.#..#.#", "#..##..#", "#......#", "########", "........"],
        ["airship"] = [".######.", "########", "########", ".######.", "...##...", "..####..", "........", "........"],
        ["swords"] = ["#......#", ".#....#.", "..#..#..", "...##...", "..####..", ".#.##.#.", "##....##", "##....##"],
        ["helmet"] = ["..####..", ".#....#.", "#..##..#", "#.#..#.#", "#.#..#.#", "#..##..#", ".######.", "#......#"],
        ["coin"] = ["..####..", ".######.", "###..###", "##....##", "##....##", "###..###", ".######.", "..####.."],
        ["plane"] = ["#.......", "####....", "#..####.", "#.....##", "#..####.", "####....", "#.......", "........"],
        ["chat"] = ["........", ".######.", "#......#", "#.#.#.##", "#......#", ".######.", ".#......", "#......."],
        ["bot"] = ["...##...", "########", "#......#", "#.#..#.#", "#......#", "#.####.#", "########", "........"],
        ["cross"] = ["#......#", ".#....#.", "..#..#..", "...##...", "...##...", "..#..#..", ".#....#.", "#......#"],
        ["minimize"] = ["........", "........", "........", "........", "........", "........", "########", "........"],
        ["arrow"] = ["........", "....#...", "....##..", "#######.", "#######.", "....##..", "....#...", "........"],
        ["back"] = ["........", "...#....", "..##....", ".#######", ".#######", "..##....", "...#....", "........"],
        ["more"] = ["........", "........", "##....##", ".##..##.", "..####..", "...##...", "........", "........"],
        ["less"] = ["........", "........", "...##...", "..####..", ".##..##.", "##....##", "........", "........"],
        ["gear"] = ["..#..#..", ".######.", "###..###", ".#....#.", ".#....#.", "###..###", ".######.", "..#..#.."],
        ["user"] = ["..####..", "..####..", "..####..", "...##...", ".######.", "########", "########", "########"],
        ["exit"] = ["####....", "#.......", "#....#..", "#.######", "#.######", "#....#..", "#.......", "####...."],
        ["folder"] = ["........", "###.....", "########", "#......#", "#......#", "#......#", "########", "........"],
        ["help"] = ["..####..", ".##..##.", ".....##.", "....##..", "...##...", "........", "...##...", "........"],
        ["book"] = ["........", "##....##", "#.#..#.#", "#..##..#", "#..##..#", "#..##..#", "########", "........"],
        ["news"] = ["########", "#......#", "#.##.#.#", "#.##...#", "#....#.#", "#.####.#", "#......#", "########"],
        ["globe"] = ["..####..", ".#.##.#.", "#..##..#", "########", "#..##..#", ".#.##.#.", "..####..", "........"],
    };

    private static readonly Dictionary<string, Geometry> Cache = new();

    static PixelIcon()
    {
        AffectsRender<PixelIcon>(KindProperty, ForegroundProperty);
    }

    public PixelIcon()
    {
        Width = 16;
        Height = 16;
        RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
    }

    public string? Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>The icon as a geometry in an 8×8 box, one rectangle per run of filled pixels in a row.</summary>
    public static Geometry? GeometryFor(string? kind)
    {
        if (kind is null || !Bitmaps.TryGetValue(kind, out var rows))
            return null;
        lock (Cache)
        {
            if (Cache.TryGetValue(kind, out var cached))
                return cached;
            var g = new StreamGeometry();
            using (var c = g.Open())
            {
                for (var y = 0; y < rows.Length; y++)
                for (var x = 0; x < rows[y].Length; x++)
                {
                    if (rows[y][x] != '#')
                        continue;
                    var start = x;
                    while (x + 1 < rows[y].Length && rows[y][x + 1] == '#')
                        x++;
                    c.BeginFigure(new Point(start, y), true);
                    c.LineTo(new Point(x + 1, y));
                    c.LineTo(new Point(x + 1, y + 1));
                    c.LineTo(new Point(start, y + 1));
                    c.EndFigure(true);
                }
            }

            Cache[kind] = g;
            return g;
        }
    }

    public override void Render(DrawingContext context)
    {
        if (GeometryFor(Kind) is not { } geometry || Foreground is not { } fg)
            return;
        var size = Math.Min(Bounds.Width, Bounds.Height);
        // whole device pixels per icon pixel when possible: 16 → 2, 24 → 3
        var scale = size >= 8 ? Math.Floor(size / 8) : size / 8;
        var offset = new Vector(Math.Floor((Bounds.Width - 8 * scale) / 2), Math.Floor((Bounds.Height - 8 * scale) / 2));
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offset)))
            context.DrawGeometry(fg, null, geometry);
    }
}
