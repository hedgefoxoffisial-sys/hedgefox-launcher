using Gml.Launcher.Core.HedgeFox;
using Xunit;

namespace HedgeFox.Launcher.Tests;

public class HardwareAdvisorTests
{
    private static HfHardware Pc(long ramGb, params (string vendor, string name)[] gpus) =>
        new(ramGb * 1024, gpus.Select(g => new HfGpu(g.vendor, g.name)).ToList());

    [Theory]
    [InlineData("NVIDIA", "NVIDIA GeForce RTX 3060", HfGpuClass.DiscreteNvidiaAmd)]
    [InlineData("NVIDIA", "NVIDIA GeForce GTX 1050 Ti", HfGpuClass.DiscreteNvidiaAmd)]
    [InlineData("Advanced Micro Devices, Inc.", "AMD Radeon RX 6600", HfGpuClass.DiscreteNvidiaAmd)]
    [InlineData("", "Advanced Micro Devices, Inc. [AMD/ATI] Navi 23 [Radeon RX 6600/6600 XT/6600M]", HfGpuClass.DiscreteNvidiaAmd)]
    [InlineData("Advanced Micro Devices, Inc.", "AMD Radeon(TM) Graphics", HfGpuClass.Integrated)]
    [InlineData("Advanced Micro Devices, Inc.", "AMD Radeon(TM) Vega 8 Graphics", HfGpuClass.Integrated)]
    [InlineData("Advanced Micro Devices, Inc.", "AMD Radeon 780M Graphics", HfGpuClass.Integrated)]
    [InlineData("", "Advanced Micro Devices, Inc. [AMD/ATI] Renoir", HfGpuClass.Integrated)]
    [InlineData("Intel Corporation", "Intel(R) UHD Graphics 620", HfGpuClass.Integrated)]
    [InlineData("Intel Corporation", "Intel(R) Iris(R) Xe Graphics", HfGpuClass.Integrated)]
    [InlineData("Intel Corporation", "Intel(R) Arc(TM) Graphics", HfGpuClass.Integrated)]
    [InlineData("Intel Corporation", "Intel(R) Arc(TM) A770 Graphics", HfGpuClass.DiscreteOther)]
    [InlineData("Apple (0x106b)", "Apple M2 Pro", HfGpuClass.AppleHigh)]
    [InlineData("Apple (0x106b)", "Apple M3 Max", HfGpuClass.AppleHigh)]
    [InlineData("Apple (0x106b)", "Apple M1 Ultra", HfGpuClass.AppleHigh)]
    [InlineData("Apple (0x106b)", "Apple M1", HfGpuClass.AppleBase)]
    [InlineData("Microsoft", "Microsoft Basic Display Adapter", HfGpuClass.Ignored)]
    [InlineData("", "Parsec Virtual Display Adapter", HfGpuClass.Ignored)]
    [InlineData("VMware, Inc.", "VMware SVGA 3D", HfGpuClass.Ignored)]
    [InlineData("", "", HfGpuClass.Ignored)]
    [InlineData("Matrox", "Matrox G200eR", HfGpuClass.Unknown)]
    public void ClassifiesGpus(string vendor, string name, HfGpuClass expected) =>
        Assert.Equal(expected, HardwareAdvisor.Classify(new HfGpu(vendor, name)));

    [Fact]
    public void EightGigabytesMeansLowPc() =>
        Assert.Equal(ProfileCatalog.LowPc, HardwareAdvisor.Advise(Pc(8, ("NVIDIA", "NVIDIA GeForce RTX 3060"))).Recommended);

    [Fact]
    public void UsableRamJustUnderEightCountsAsEight() =>
        Assert.Equal(ProfileCatalog.LowPc,
            HardwareAdvisor.Advise(new HfHardware(7982, [new HfGpu("NVIDIA", "NVIDIA GeForce GTX 1650")])).Recommended);

    [Fact]
    public void NoDiscreteCardMeansLowPcEvenWithLotsOfRam()
    {
        var advice = HardwareAdvisor.Advise(Pc(32, ("Intel Corporation", "Intel(R) UHD Graphics 630")));
        Assert.Equal(ProfileCatalog.LowPc, advice.Recommended);
        Assert.False(advice.ShadersOk);
    }

    [Fact]
    public void LaptopWithIntegratedAndNvidiaCountsTheNvidia()
    {
        var advice = HardwareAdvisor.Advise(Pc(16, ("Intel Corporation", "Intel(R) UHD Graphics"),
            ("NVIDIA", "NVIDIA GeForce RTX 4060 Laptop GPU")));
        Assert.Equal(ProfileCatalog.Normal, advice.Recommended);
        Assert.True(advice.ShadersOk);
    }

    [Fact]
    public void SixteenGigabytesAndDiscreteAmdAllowShaders()
    {
        var advice = HardwareAdvisor.Advise(Pc(16, ("Advanced Micro Devices, Inc.", "AMD Radeon RX 7800 XT")));
        Assert.Equal(ProfileCatalog.Normal, advice.Recommended);
        Assert.True(advice.ShadersOk);
    }

    [Fact]
    public void TwelveGigabytesAndDiscreteIsNormalWithoutShaders()
    {
        var advice = HardwareAdvisor.Advise(Pc(12, ("NVIDIA", "NVIDIA GeForce GTX 1660")));
        Assert.Equal(ProfileCatalog.Normal, advice.Recommended);
        Assert.False(advice.ShadersOk);
    }

    [Fact]
    public void AppleProWithSixteenGigabytesAllowsShaders() =>
        Assert.True(HardwareAdvisor.Advise(Pc(16, ("Apple (0x106b)", "Apple M2 Pro"))).ShadersOk);

    [Fact]
    public void AppleProWithEightGigabytesIsLowPc() =>
        Assert.Equal(ProfileCatalog.LowPc, HardwareAdvisor.Advise(Pc(8, ("Apple (0x106b)", "Apple M2 Pro"))).Recommended);

    [Fact]
    public void AppleBaseChipWithSixteenGigabytesIsNormal()
    {
        var advice = HardwareAdvisor.Advise(Pc(16, ("Apple (0x106b)", "Apple M1")));
        Assert.Equal(ProfileCatalog.Normal, advice.Recommended);
        Assert.False(advice.ShadersOk);
    }

    [Fact]
    public void IntelArcIsNotIntegratedButNotForShaders()
    {
        var advice = HardwareAdvisor.Advise(Pc(16, ("Intel Corporation", "Intel(R) Arc(TM) A750 Graphics")));
        Assert.Equal(ProfileCatalog.Normal, advice.Recommended);
        Assert.False(advice.ShadersOk);
    }

    [Fact]
    public void UnknownGpuFallsBackToRamOnly()
    {
        Assert.Equal(ProfileCatalog.Normal, HardwareAdvisor.Advise(Pc(16)).Recommended);
        Assert.Equal(ProfileCatalog.LowPc, HardwareAdvisor.Advise(Pc(8)).Recommended);
        Assert.False(HardwareAdvisor.Advise(Pc(32)).ShadersOk);
    }

    [Fact]
    public void VirtualAdaptersAreIgnored()
    {
        var advice = HardwareAdvisor.Advise(Pc(16, ("Microsoft", "Microsoft Basic Display Adapter"),
            ("NVIDIA", "NVIDIA GeForce RTX 3070")));
        Assert.True(advice.ShadersOk);
    }

    [Fact]
    public void UnknownRamGivesNoAdvice()
    {
        var advice = HardwareAdvisor.Advise(new HfHardware(0, [new HfGpu("NVIDIA", "NVIDIA GeForce RTX 3060")]));
        Assert.Null(advice.Recommended);
        Assert.False(advice.ShadersOk);
    }
}
