using Avalonia;
using Gml.Launcher.Views.HedgeFox;
using Xunit;

namespace HedgeFox.Launcher.Tests;

/// <summary>
///     The 1180×720 window on a laptop at 125–150 % (code-3): Avalonia centres a window taller than the work area at a
///     negative Y, so the title bar starts above the screen and the footer sits under the taskbar.
/// </summary>
public class WindowFitTests
{
    private static readonly Size Wanted = new(1180, 720);
    private static readonly Size Min = new(1000, 620);

    [Fact]
    public void A1080pLaptopAt150PercentGetsAShorterWindowInsideTheWorkArea()
    {
        // 1920×1080 px, Windows 11 taskbar 72 px: 1280×672 logical
        var work = new PixelRect(0, 0, 1920, 1008);
        var fit = HfWindowFit.Fit(Wanted, Wanted, Min, new PixelPoint(75, -36), work, 1.5);

        Assert.NotNull(fit);
        Assert.Equal(1180, fit.Value.Width);
        Assert.Equal(672, fit.Value.Height);
        Assert.Equal(new PixelPoint(75, 0), fit.Value.Position);
        Assert.Equal(1000, fit.Value.MinWidth);
        Assert.Equal(620, fit.Value.MinHeight);
    }

    [Fact]
    public void ASmallScreenLowersTheMinimumToWhatFits()
    {
        // 1366×768 px at 125 % with a 40 px taskbar: about 1092×582 logical, below the 620 minimum
        var work = new PixelRect(0, 0, 1366, 728);
        var fit = HfWindowFit.Fit(Wanted, Wanted, Min, new PixelPoint(-54, -86), work, 1.25)!.Value;

        Assert.Equal(1092, fit.Width);
        Assert.Equal(582, fit.Height);
        Assert.Equal(1000, fit.MinWidth);
        Assert.Equal(582, fit.MinHeight);
        Assert.True(fit.Position.X >= 0 && fit.Position.Y >= 0);
        Assert.True(fit.Position.X + fit.Width * 1.25 <= 1366 && fit.Position.Y + fit.Height * 1.25 <= 728);
    }

    [Fact]
    public void TheWorkAreaMayStartAnywhere()
    {
        // a second monitor to the right, taskbar at the top
        var work = new PixelRect(1920, 40, 1600, 860);
        var fit = HfWindowFit.Fit(Wanted, Wanted, Min, new PixelPoint(2130, 30), work, 1.0)!.Value;
        Assert.Equal(new PixelPoint(1920 + 210, 40 + 70), fit.Position);
        Assert.Equal(720, fit.Height);
    }

    [Fact]
    public void AFrameAroundTheContentIsLeftRoomFor()
    {
        // a system title bar of 28 px over a 720 px client on a 740 px work area
        var work = new PixelRect(0, 0, 1440, 740);
        var fit = HfWindowFit.Fit(Wanted, new Size(1180, 748), Min, new PixelPoint(130, -4), work, 1.0)!.Value;
        Assert.Equal(712, fit.Height);
        Assert.Equal(0, fit.Position.Y);
    }

    [Fact]
    public void AWindowThatFitsIsLeftAlone()
    {
        var work = new PixelRect(0, 0, 2560, 1400);
        Assert.Null(HfWindowFit.Fit(Wanted, Wanted, Min, new PixelPoint(690, 340), work, 1.0));
    }
}
