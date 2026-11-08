using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Gml.Launcher.Core.HedgeFox;

public sealed record HfGpu(string Vendor, string Name);

/// <summary>What the advice is based on: total memory and the video adapters the OS reports.</summary>
public sealed record HfHardware(long TotalRamMb, IReadOnlyList<HfGpu> Gpus)
{
    public static readonly HfHardware Unknown = new(0, []);
}

public enum HfGpuClass
{
    /// <summary>Not a GPU we can judge (unknown vendor).</summary>
    Unknown,

    /// <summary>Virtual or remote display adapters: not counted at all.</summary>
    Ignored,

    /// <summary>Intel graphics and AMD APUs: no discrete card.</summary>
    Integrated,

    /// <summary>A discrete NVIDIA or AMD card: shaders are fine with enough memory.</summary>
    DiscreteNvidiaAmd,

    /// <summary>Other discrete cards (Intel Arc A-series): not integrated, but not counted for shaders.</summary>
    DiscreteOther,

    /// <summary>Apple M* Pro, Max or Ultra.</summary>
    AppleHigh,

    /// <summary>Apple M* base chips.</summary>
    AppleBase
}

/// <param name="Recommended">GML name of the profile that gets «Советуем для твоего ПК», or null when unknown.</param>
/// <param name="ShadersOk">«Шейдеры» also suit this PC.</param>
public sealed record HfAdvice(string? Recommended, bool ShadersOk);

/// <summary>
///     The hardware tip on the profile cards (spec «Карточки профилей»). Pure: give it a snapshot.
///     RAM ≤ 8 GB or no discrete card (Intel, integrated AMD) → «Слабый ПК»;
///     RAM ≥ 16 GB and a discrete NVIDIA/AMD, or Apple M* Pro/Max/Ultra with ≥ 16 GB → «Обычный», shaders also fine;
///     otherwise «Обычный». The player's choice always wins over the advice.
/// </summary>
public static class HardwareAdvisor
{
    private static readonly Regex Virtual = new(
        @"basic (display|render)|virtual|vmware|virtualbox|parallels|citrix|remote|parsec|llvmpipe|swiftshader|" +
        @"qxl|red hat|spice|displaylink|hyper-v|mirror driver",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // AMD APUs: "Radeon(TM) Graphics", "Vega 8 Graphics", "780M", "Radeon R7 Graphics", Linux codenames
    private static readonly Regex AmdIntegrated = new(
        @"radeon(\(tm\))? graphics|vega \d+ graphics|\b\d{3}m\b|radeon r\d graphics|" +
        @"renoir|cezanne|lucienne|picasso|raven|rembrandt|phoenix|barcelo|mendocino|van gogh|hawk point|strix|" +
        @"kaveri|carrizo|stoney|beema|kabini|mullins",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex IntelDiscrete = new(@"arc(\(tm\))? a\d{3}|arc(\(tm\))? b\d{3}|\bdg[12]\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AppleHighChip = new(@"\b(pro|max|ultra)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static HfGpuClass Classify(HfGpu gpu)
    {
        var text = $"{gpu.Vendor} {gpu.Name}".Trim();
        if (text.Length == 0 || Virtual.IsMatch(text))
            return HfGpuClass.Ignored;

        var t = text.ToLowerInvariant();
        if (t.Contains("apple"))
            return AppleHighChip.IsMatch(gpu.Name) ? HfGpuClass.AppleHigh : HfGpuClass.AppleBase;
        if (t.Contains("nvidia") || t.Contains("geforce") || t.Contains("quadro") || t.Contains("tesla"))
            return HfGpuClass.DiscreteNvidiaAmd;
        if (t.Contains("amd") || t.Contains("radeon") || t.Contains("ati technologies") || t.Contains("[ati"))
            return AmdIntegrated.IsMatch(gpu.Name) && !Regex.IsMatch(gpu.Name, @"\brx\s?\d", RegexOptions.IgnoreCase)
                ? HfGpuClass.Integrated
                : HfGpuClass.DiscreteNvidiaAmd;
        if (t.Contains("intel"))
            return IntelDiscrete.IsMatch(gpu.Name) ? HfGpuClass.DiscreteOther : HfGpuClass.Integrated;
        return HfGpuClass.Unknown;
    }

    public static HfAdvice Advise(HfHardware hardware)
    {
        if (hardware.TotalRamMb <= 0)
            return new HfAdvice(null, false);

        // 8 GB machines report a little less (7.8 GB usable), so whole gigabytes are compared
        var ramGb = Math.Round(hardware.TotalRamMb / 1024.0);
        var classes = hardware.Gpus.Select(Classify)
            .Where(c => c is not (HfGpuClass.Ignored or HfGpuClass.Unknown)).ToList();
        var onlyIntegrated = classes.Count > 0 && classes.All(c => c == HfGpuClass.Integrated);

        if (ramGb <= 8 || onlyIntegrated)
            return new HfAdvice(ProfileCatalog.LowPc, false);

        var strongGpu = classes.Any(c => c is HfGpuClass.DiscreteNvidiaAmd or HfGpuClass.AppleHigh);
        return new HfAdvice(ProfileCatalog.Normal, ramGb >= 16 && strongGpu);
    }
}
