using Avalonia.Media;
using Gml.Launcher.Core.HedgeFox;
using Xunit;

namespace HedgeFox.Launcher.Tests;

/// <summary>
///     The stock view models colour their toasts with hex fills; the HedgeFox toast keeps the site's night plate and
///     turns the fill into the plate's left bar: red tones are errors, green is success, the rest is information.
/// </summary>
public class ToastTests
{
    [Theory]
    [InlineData("#D03E3E", HfToastTone.Error)] // PageViewModelBase.ShowError, the login failures
    [InlineData("#993030", HfToastTone.Error)] // offline mode
    [InlineData("#933", HfToastTone.Error)] // «server still down»
    [InlineData("#A54848", HfToastTone.Error)]
    [InlineData("#008C45", HfToastTone.Success)] // ShowSuccess
    [InlineData("#086", HfToastTone.Success)] // «server is back»
    [InlineData("#3684EA", HfToastTone.Info)] // VPN
    [InlineData("#222", HfToastTone.Info)] // «checking the server»
    [InlineData("#111111", HfToastTone.Info)] // reconnecting
    [InlineData("#F15B19", HfToastTone.Info)] // orange is not an error
    public void TheStockFillPicksTheTone(string fill, HfToastTone tone) =>
        Assert.Equal(tone, HfToastTones.For(Color.Parse(fill)));

    [Fact]
    public void NoFillIsInformation() => Assert.Equal(HfToastTone.Info, HfToastTones.For(null));
}
