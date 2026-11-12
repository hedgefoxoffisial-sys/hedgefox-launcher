using Gml.Launcher.Core.HedgeFox;
using Xunit;

namespace HedgeFox.Launcher.Tests;

public class ProfileCatalogTests
{
    [Theory]
    [InlineData("HedgeFox", "Обычный", "Полная дальность и все корабли. Для большинства ПК.", 6144)]
    [InlineData("HedgeFox-LowPC", "Слабый ПК", "Короче дальность, легче графика. Для ноутбуков и ПК до 8 ГБ.", 4096)]
    [InlineData("HedgeFox-Shaders", "Шейдеры", "Красиво: живой свет и вода. Нужна мощная видеокарта.", 8192)]
    public void KnownProfilesUseTheSpecTable(string gmlName, string title, string hint, int ramMb)
    {
        var card = ProfileCatalog.For(gmlName, "HedgeFox · что-то из GML", 1024);
        Assert.True(card.Known);
        Assert.Equal(gmlName, card.GmlName);
        Assert.Equal(title, card.Title);
        Assert.Equal(hint, card.Hint);
        Assert.Equal(ramMb, card.RamMb);
    }

    [Fact]
    public void GmlNamesMatchWithoutCase() => Assert.Equal("Слабый ПК", ProfileCatalog.For("hedgefox-lowpc", null).Title);

    [Fact]
    public void UnknownProfileShowsItsGmlDisplayName()
    {
        var card = ProfileCatalog.For("HedgeFox-Event", "Ивент: Великий прилив", 5120);
        Assert.False(card.Known);
        Assert.Equal("Ивент: Великий прилив", card.Title);
        Assert.Equal("", card.Hint);
        Assert.Equal(5120, card.RamMb);
    }

    [Fact]
    public void UnknownProfileWithoutDisplayNameShowsItsName() =>
        Assert.Equal("HedgeFox-Event", ProfileCatalog.For("HedgeFox-Event", "  ", 0).Title);

    [Fact]
    public void CardsComeInTheTableOrderThenTheRest()
    {
        var ordered = ProfileCatalog.Order(new[] { "HedgeFox-Shaders", "Zeta", "HedgeFox", "HedgeFox-LowPC" }, n => n);
        Assert.Equal(new[] { "HedgeFox", "HedgeFox-LowPC", "HedgeFox-Shaders", "Zeta" }, ordered);
    }

    [Theory]
    [InlineData(6144, "6 ГБ")]
    [InlineData(4096, "4 ГБ")]
    [InlineData(1536, "1,5 ГБ")]
    [InlineData(0, "")]
    public void RamLabel(int mb, string label) => Assert.Equal(label, ProfileCatalog.RamLabel(mb));
}
