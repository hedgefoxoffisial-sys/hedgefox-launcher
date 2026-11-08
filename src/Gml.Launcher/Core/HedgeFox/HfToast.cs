using Avalonia.Media;

namespace Gml.Launcher.Core.HedgeFox;

/// <summary>What a toast is about: its left bar is HfError, HfSuccess or HfPurple.</summary>
public enum HfToastTone
{
    Info,
    Error,
    Success
}

/// <summary>
///     The stock view models pass hex fills to their toasts (#D03E3E errors, #008C45 success, #3684EA the VPN note,
///     greys for «checking»). The HedgeFox toast keeps the site's night plate and maps that fill to a tone.
/// </summary>
public static class HfToastTones
{
    public static HfToastTone For(Color? fill)
    {
        if (fill is not { } c)
            return HfToastTone.Info;
        var hsv = c.ToHsv();
        // greys and near-black fills carry no meaning
        if (hsv.S < 0.25 || hsv.V < 0.2)
            return HfToastTone.Info;
        if (hsv.H <= 12 || hsv.H >= 340)
            return HfToastTone.Error;
        return hsv.H is >= 90 and <= 170 ? HfToastTone.Success : HfToastTone.Info;
    }
}
