using System;
using System.Collections.Generic;
using System.Linq;

namespace Gml.Launcher.Core.HedgeFox;

/// <summary>Shade of one cell of a pixel cog: light tips, body, dark hub, pale shaft.</summary>
public enum GearTone
{
    None,
    Tip,
    Body,
    Hub,
    Shaft
}

/// <param name="Teeth">Number of teeth.</param>
/// <param name="Pitch">Pitch radius in cells; two cogs mesh when their pitch radii add up to the distance between centres.</param>
/// <param name="Addendum">Tooth height above and below the pitch circle.</param>
/// <param name="Hub">Radius of the dark hub around the shaft (default: 35 % of the pitch, at least 2).</param>
/// <param name="Shaft">Side of the square shaft in the middle.</param>
/// <param name="Windows">Round windows through the disc, like the spokes of a large cogwheel.</param>
public sealed record PixelGearOptions(
    int Teeth,
    double Pitch,
    double Addendum = 2,
    double? Hub = null,
    double Shaft = 4,
    int Windows = 0);

/// <summary>
///     The site's pixel cogwheels (web/site/src/lib/pixel-gear.ts), cell for cell: a Create-style cog rasterised on a
///     small grid. Shading is radial, so it stays right at any angle while the cog turns.
/// </summary>
public static class PixelGear
{
    private const double Tau = Math.PI * 2;

    /// <summary>The cog's cells as [row, column]; the centre of the cog is the middle of the grid.</summary>
    public static GearTone[,] Raster(PixelGearOptions o)
    {
        var add = o.Addendum;
        var outer = o.Pitch + add;
        var root = o.Pitch - add;
        var hub = o.Hub ?? Math.Max(2, Math.Round(o.Pitch * 0.35, MidpointRounding.AwayFromZero));
        var shaft = o.Shaft;
        var size = (int)(2 * Math.Ceiling(outer) + 2);
        var c = size / 2.0;
        var toothPitch = Tau / o.Teeth;
        var grid = new GearTone[size, size];
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var px = x + 0.5 - c;
            var py = y + 0.5 - c;
            var r = Hypot(px, py);
            // angle from straight up, clockwise; a tooth is centred on angle 0
            var a = (Math.Atan2(px, -py) + Tau) % Tau;
            var tone = GearTone.None;
            if (Math.Abs(px) < shaft / 2 && Math.Abs(py) < shaft / 2)
                tone = GearTone.Shaft;
            else if (r < hub)
                tone = GearTone.Hub;
            else if (r <= root)
            {
                tone = GearTone.Body;
                if (o.Windows > 0)
                {
                    // round holes on the middle circle, between the spokes
                    var wp = Tau / o.Windows;
                    var theta = (Math.Floor(a / wp) + 0.5) * wp;
                    var mid = (hub + root) / 2;
                    var radius = Math.Min((root - hub) / 2 - 1.5, wp * mid * 0.3);
                    if (Hypot(px - mid * Math.Sin(theta), py + mid * Math.Cos(theta)) < radius)
                        tone = GearTone.None;
                }

                if (tone != GearTone.None && r > root - 1.25)
                    tone = GearTone.Tip;
            }
            else if (r <= outer)
            {
                // trapezoid teeth: half a pitch wide at the root, a third at the tip
                var t = (r - root) / (outer - root);
                var half = (0.27 - 0.1 * t) * toothPitch;
                var off = Math.Abs((a + toothPitch / 2) % toothPitch - toothPitch / 2);
                if (off <= half)
                    tone = GearTone.Tip;
            }

            grid[y, x] = tone;
        }

        return grid;
    }

    // Math.hypot in JavaScript: no overflow tricks needed for a grid this small
    private static double Hypot(double x, double y) => Math.Sqrt(x * x + y * y);
}

/// <param name="Options">The cog's shape.</param>
/// <param name="X">Centre, in cells, relative to the big cog.</param>
/// <param name="Y">Centre, in cells, relative to the big cog.</param>
/// <param name="Phase">Starting rotation in degrees, so the teeth interleave.</param>
/// <param name="TurnSeconds">Time of one full turn.</param>
/// <param name="Reverse">Turns anticlockwise.</param>
/// <param name="Tip">Colour of the tips (#RRGGBB).</param>
/// <param name="Body">Colour of the disc.</param>
/// <param name="Hub">Colour of the hub.</param>
/// <param name="Shaft">Colour of the shaft.</param>
/// <param name="Side">Colour of the extruded side drawn one cell down and right.</param>
public sealed record HfCog(
    PixelGearOptions Options,
    double X,
    double Y,
    double Phase,
    double TurnSeconds,
    bool Reverse,
    string Tip,
    string Body,
    string Hub,
    string Shaft,
    string Side);

/// <summary>
///     The loader's three meshing cogs (web/site/src/components/intro/PixelGears.astro): a large purple cogwheel with
///     spokes, a small orange cog and a brass one. Equal tooth spacing lets them mesh; speeds follow the tooth counts.
/// </summary>
public static class HfCogs
{
    private const double BigPitch = 15;

    public static IReadOnlyList<HfCog> Loader { get; } = Build();

    private static IReadOnlyList<HfCog> Build()
    {
        var small = At(60, BigPitch + 10);
        var tiny = At(210, BigPitch + 7.5);
        return
        [
            new HfCog(new PixelGearOptions(12, BigPitch, Hub: 5, Windows: 6), 0, 0, 0, 6, false,
                "#B39BFF", "#9959F4", "#6A3FD1", "#E9E1F7", "#2E1B5E"),
            // a tooth of the big cog points at 60°, so the small one turns a gap towards it (240° − 22.5° − 180° = 37.5°)
            new HfCog(new PixelGearOptions(8, 10, Hub: 4), small.X, small.Y, 37.5, 4, true,
                "#FFC48A", "#FF9D45", "#D9701F", "#FFF1DF", "#7A3A0C"),
            new HfCog(new PixelGearOptions(6, 7.5, Hub: 3), tiny.X, tiny.Y, 0, 3, true,
                "#FFD27A", "#F0A33A", "#B8792A", "#FFF4D6", "#5C4418")
        ];
    }

    private static (double X, double Y) At(double degrees, double distance) =>
        (distance * Math.Sin(degrees * Math.PI / 180), -distance * Math.Cos(degrees * Math.PI / 180));

    /// <summary>Side of a cog's grid in cells.</summary>
    public static int SizeOf(PixelGearOptions o) => (int)(2 * Math.Ceiling(o.Pitch + o.Addendum) + 2);

    /// <summary>The box around all cogs with 2 cells of room and 2 more for the extruded side (the site's viewBox).</summary>
    public static (int MinX, int MinY, int Width, int Height) Bounds(IEnumerable<HfCog> cogs)
    {
        const int pad = 2;
        var list = cogs.ToList();
        var minX = (int)Math.Floor(list.Min(c => c.X - SizeOf(c.Options) / 2.0)) - pad;
        var minY = (int)Math.Floor(list.Min(c => c.Y - SizeOf(c.Options) / 2.0)) - pad;
        var maxX = (int)Math.Ceiling(list.Max(c => c.X + SizeOf(c.Options) / 2.0)) + pad + 2;
        var maxY = (int)Math.Ceiling(list.Max(c => c.Y + SizeOf(c.Options) / 2.0)) + pad + 2;
        return (minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>Rotation in degrees <paramref name="seconds" /> after start (clockwise is positive).</summary>
    public static double Angle(HfCog cog, double seconds) =>
        cog.Phase + (cog.Reverse ? -1 : 1) * 360 * seconds / cog.TurnSeconds;
}
