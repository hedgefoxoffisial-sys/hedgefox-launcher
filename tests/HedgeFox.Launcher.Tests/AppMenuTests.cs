using System.Text.RegularExpressions;
using Gml.Launcher.Core.HedgeFox;
using Xunit;

namespace HedgeFox.Launcher.Tests;

/// <summary>
///     The macOS menu bar's app menu (copy-lore-1): Avalonia's default one says «About Avalonia», «Hide Application»,
///     «Quit» in English. Ours is Russian and only what a player needs.
/// </summary>
public class AppMenuTests
{
    [Fact]
    public void TheAppMenuIsRussianWithTheUsualShortcuts()
    {
        var items = HfAppMenu.Items;
        Assert.Equal(["Скрыть HedgeFox", "Выйти из HedgeFox"], items.Select(i => i.Header));
        Assert.Equal(["Cmd+H", "Cmd+Q"], items.Select(i => i.Gesture));
        Assert.Equal([HfAppMenuAction.Hide, HfAppMenuAction.Quit], items.Select(i => i.Action));
        foreach (var item in items)
            Assert.DoesNotMatch("Avalonia|About|Quit|Hide", item.Header);
    }

    [Fact]
    public void OnlyTheBrandIsLatin()
    {
        foreach (var item in HfAppMenu.Items)
            Assert.Matches(new Regex("^[А-Яа-яЁё ]+HedgeFox$"), item.Header);
    }
}
