using System;
using System.Linq;
using System.Threading.Tasks;
using Hardware.Info;

namespace Gml.Launcher.Core.HedgeFox;

public interface IHardwareProbe
{
    /// <summary>Total memory and video adapters of this PC, read once (WMI on Windows takes a moment). Never throws.</summary>
    Task<HfHardware> ReadAsync();
}

/// <summary>Hardware.Info: memory and video controllers. Feed the result to <see cref="HardwareAdvisor.Advise" />.</summary>
public sealed class HardwareProbe : IHardwareProbe
{
    private readonly Lazy<Task<HfHardware>> _snapshot = new(() => Task.Run(Read));

    public Task<HfHardware> ReadAsync() => _snapshot.Value;

    private static HfHardware Read()
    {
        try
        {
            var info = new HardwareInfo();
            info.RefreshMemoryStatus();
            info.RefreshVideoControllerList();
            var gpus = info.VideoControllerList
                .Select(v => new HfGpu(v.Manufacturer ?? string.Empty, v.Name ?? string.Empty))
                .ToList();
            return new HfHardware((long)(info.MemoryStatus.TotalPhysical / (1024 * 1024)), gpus);
        }
        catch (Exception)
        {
            return HfHardware.Unknown;
        }
    }
}
