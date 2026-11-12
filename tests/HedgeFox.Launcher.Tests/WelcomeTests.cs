using Gml.Dto.Profile;
using Gml.Launcher.Core.HedgeFox;
using Xunit;

namespace HedgeFox.Launcher.Tests;

public class WelcomeTests
{
    private static HfProfileCard Card(string gml) =>
        new(new ProfileReadDto { Name = gml }, gml, gml, string.Empty, "6 ГБ", false, false, false);

    [Fact]
    public void MarkedTextSplitsOnStars()
    {
        var parts = HfMarked.Parse("Дальность *7 чанков*, без *облаков*");
        Assert.Equal(
            [new HfMarkedPart("Дальность ", false), new HfMarkedPart("7 чанков", true), new HfMarkedPart(", без ", false),
             new HfMarkedPart("облаков", true)],
            parts);
    }

    [Fact]
    public void AnUnclosedStarStaysText() =>
        Assert.Equal([new HfMarkedPart("5 * 3", false)], HfMarked.Parse("5 * 3"));

    [Fact]
    public void EmptyMarkupHasNoParts() => Assert.Empty(HfMarked.Parse(string.Empty));

    [Fact]
    public void ColumnsGoWeakNormalShaders()
    {
        var cards = new[] { Card(ProfileCatalog.Normal), Card("Other"), Card(ProfileCatalog.Shaders), Card(ProfileCatalog.LowPc) };
        Assert.Equal([ProfileCatalog.LowPc, ProfileCatalog.Normal, ProfileCatalog.Shaders, "Other"],
            HfWelcome.Order(cards).Select(c => c.GmlName));
    }

    [Fact]
    public void EachKnownVersionHasMarkedPointsWithItsMemory()
    {
        foreach (var gml in new[] { ProfileCatalog.LowPc, ProfileCatalog.Normal, ProfileCatalog.Shaders })
        {
            var points = HfWelcome.Points(gml, "6 ГБ");
            Assert.InRange(points.Count, 3, 4);
            Assert.All(points, p => Assert.Contains(HfMarked.Parse(p), part => part.Marked));
            Assert.Contains(points, p => p.Contains("*6 ГБ*"));
        }
    }

    [Fact]
    public void PointsTellTheRenderDistanceOfEachVersion()
    {
        Assert.Contains(HfWelcome.Points(ProfileCatalog.LowPc, "4 ГБ"), p => p.Contains("7 чанков"));
        Assert.Contains(HfWelcome.Points(ProfileCatalog.Normal, "6 ГБ"), p => p.Contains("12 чанков"));
        Assert.Contains(HfWelcome.Points(ProfileCatalog.Shaders, "8 ГБ"), p => p.Contains("10 чанков"));
    }

    [Fact]
    public void WithoutMemoryTheMemoryPointIsLeftOut() =>
        Assert.DoesNotContain(HfWelcome.Points(ProfileCatalog.Normal, string.Empty), p => p.Contains("памяти"));

    [Fact]
    public void AnUnknownVersionHasNoPoints() => Assert.Empty(HfWelcome.Points("Other", "6 ГБ"));

    [Fact]
    public void PcLineNamesTheCardAndRoundedMemory() =>
        Assert.Equal("Intel UHD Graphics 620 · 8 ГБ памяти",
            HfWelcome.PcLine(new HfHardware(7_900, [new HfGpu("Intel Corporation", "Intel(R) UHD Graphics 620")])));

    [Fact]
    public void PcLinePrefersTheDiscreteCardAndSkipsVirtualOnes() =>
        Assert.Equal("NVIDIA GeForce RTX 3060 · 16 ГБ памяти",
            HfWelcome.PcLine(new HfHardware(16_384, [
                new HfGpu("", "Parsec Virtual Display Adapter"),
                new HfGpu("Intel Corporation", "Intel(R) UHD Graphics 770"),
                new HfGpu("NVIDIA", "NVIDIA GeForce RTX 3060")
            ])));

    [Fact]
    public void PcLineWithOnlyMemory() =>
        Assert.Equal("32 ГБ памяти", HfWelcome.PcLine(new HfHardware(32_768, [])));

    [Fact]
    public void PcLineIsEmptyWhenNothingIsKnown() => Assert.Equal(string.Empty, HfWelcome.PcLine(HfHardware.Unknown));

    [Fact]
    public void EachVersionHasItsOwnPicture()
    {
        var pictures = new[] { ProfileCatalog.LowPc, ProfileCatalog.Normal, ProfileCatalog.Shaders }.Select(HfWelcome.Picture).ToList();
        Assert.Equal(3, pictures.Distinct().Count());
        Assert.All(pictures, p => Assert.StartsWith("avares://Gml.Launcher/Assets/HedgeFox/Images/Welcome/", p));
        Assert.Equal(HfWelcome.Picture(ProfileCatalog.Normal), HfWelcome.Picture("Other"));
    }
}
