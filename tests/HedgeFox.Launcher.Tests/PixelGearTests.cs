using System.Security.Cryptography;
using System.Text;
using Gml.Launcher.Core.HedgeFox;
using Xunit;

namespace HedgeFox.Launcher.Tests;

/// <summary>
///     The splash cogs are the site's (web/site/src/lib/pixel-gear.ts, components/intro/PixelGears.astro). The
///     expected grids and hashes were printed by running that TypeScript under node.
/// </summary>
public class PixelGearTests
{
    private static string Rows(GearTone[,] grid)
    {
        var sb = new StringBuilder();
        for (var y = 0; y < grid.GetLength(0); y++)
        {
            if (y > 0)
                sb.Append('\n');
            for (var x = 0; x < grid.GetLength(1); x++)
                sb.Append(grid[y, x] switch
                {
                    GearTone.Tip => 't',
                    GearTone.Body => 'b',
                    GearTone.Hub => 'h',
                    GearTone.Shaft => 's',
                    _ => '.'
                });
        }

        return sb.ToString();
    }

    private static string Sha256(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    [Fact]
    public void TinyCogMatchesTheSite()
    {
        string[] expected =
        [
            "......................",
            "......................",
            ".........tttt.........",
            ".........tttt.........",
            ".........tttt.........",
            "...tt....tttt....tt...",
            "...ttt..tttttt..ttt...",
            "..tttttttbbbbttttttt..",
            "...tttttbhhhhbttttt...",
            ".....ttbhsssshbtt.....",
            "......tbhsssshbt......",
            "......tbhsssshbt......",
            ".....ttbhsssshbtt.....",
            "...tttttbhhhhbttttt...",
            "..tttttttbbbbttttttt..",
            "...ttt..tttttt..ttt...",
            "...tt....tttt....tt...",
            ".........tttt.........",
            ".........tttt.........",
            ".........tttt.........",
            "......................",
            "......................"
        ];
        var grid = PixelGear.Raster(new PixelGearOptions(6, 7.5, Hub: 3));
        Assert.Equal(string.Join("\n", expected), Rows(grid));
    }

    [Theory]
    [InlineData(12, 15, 5, 6, 36, "7835031bc514c9ccfa1f3f9cfbecc26d30aaa0fb267e9efe513609c6faa4ac1a")]
    [InlineData(8, 10, 4, 0, 26, "735ccd28d1e37ebc6f844bf4b1e76e334c222213ae63b93f48b54a5755a101db")]
    [InlineData(6, 7.5, 3, 0, 22, "919ded5367aa3019c787e0525dfb6cde8ddd8c662b297413a1a6693042354e67")]
    public void CogsAreTheSitesCellForCell(int teeth, double pitch, double hub, int windows, int size, string sha)
    {
        var grid = PixelGear.Raster(new PixelGearOptions(teeth, pitch, Hub: hub, Windows: windows));
        Assert.Equal(size, grid.GetLength(0));
        Assert.Equal(size, grid.GetLength(1));
        Assert.Equal(sha, Sha256(Rows(grid)));
    }

    [Fact]
    public void LoaderHasThreeMeshingCogsLikeTheSite()
    {
        var cogs = HfCogs.Loader;
        Assert.Equal(3, cogs.Count);
        Assert.Equal((0.0, 0.0), (cogs[0].X, cogs[0].Y));
        Assert.Equal(21.650635, cogs[1].X, 5);
        Assert.Equal(-12.5, cogs[1].Y, 5);
        Assert.Equal(-11.25, cogs[2].X, 5);
        Assert.Equal(19.485572, cogs[2].Y, 5);
        // pitch circles touch: the distance between centres is the sum of the pitch radii
        foreach (var other in cogs.Skip(1))
            Assert.Equal(cogs[0].Options.Pitch + other.Options.Pitch,
                Math.Sqrt(other.X * other.X + other.Y * other.Y), 6);
        Assert.False(cogs[0].Reverse);
        Assert.True(cogs[1].Reverse);
        Assert.True(cogs[2].Reverse);
    }

    [Fact]
    public void TeethPassAtTheSameRateSoTheCogsMesh()
    {
        var perTooth = HfCogs.Loader.Select(c => c.TurnSeconds / c.Options.Teeth).Distinct().ToList();
        Assert.Single(perTooth);
    }

    [Fact]
    public void LoaderBoundsMatchTheSitesViewBox()
    {
        var b = HfCogs.Bounds(HfCogs.Loader);
        Assert.Equal((-25, -28, 64, 63), (b.MinX, b.MinY, b.Width, b.Height));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1.5, 90)]
    [InlineData(6, 360)]
    public void BigCogTurnsOnceInSixSeconds(double seconds, double degrees) =>
        Assert.Equal(degrees, HfCogs.Angle(HfCogs.Loader[0], seconds), 6);

    [Fact]
    public void SmallCogsTurnTheOtherWayFromTheirPhase()
    {
        var small = HfCogs.Loader[1];
        Assert.Equal(37.5, HfCogs.Angle(small, 0), 6);
        Assert.Equal(37.5 - 90, HfCogs.Angle(small, 1), 6);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1.59, 0)]
    [InlineData(1.6, 1)]
    [InlineData(3.3, 2)]
    // the site's loader (intro-ui.ts) stops on the last hint: «ПОЧТИ ГОТОВО…» never goes back to the first one
    [InlineData(4.8, 2)]
    [InlineData(60, 2)]
    public void SplashHintsChangeEveryOnePointSixSecondsAndStopAtTheLast(double seconds, int index) =>
        Assert.Equal(HfStrings.SplashHints[index], HfStrings.SplashHintAt(TimeSpan.FromSeconds(seconds)));

    // the stock checks' own status, shown only when the sign-in server is slow or down (code-5)
    [Fact]
    public void TheSplashSaysWhenTheSignInServerIsSlowOrDown()
    {
        const string slow = "Всё еще проверка сервера", down = "Сервер не отвечает";
        var at = TimeSpan.FromSeconds(1.7);
        Assert.Equal(HfStrings.SplashHintAt(at), HfStrings.SplashLine("Проверка сервера", at, slow, down));
        Assert.Equal(HfStrings.SplashHintAt(at), HfStrings.SplashLine(null, at, slow, down));
        Assert.Equal(HfStrings.SplashSlow, HfStrings.SplashLine(slow, at, slow, down));
        Assert.Equal(HfStrings.SplashOffline, HfStrings.SplashLine(down, at, slow, down));
    }
}
