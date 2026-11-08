using System;
using Avalonia.Controls;

namespace Gml.Launcher.Views.HedgeFox.Controls;

/// <summary>
///     The site's chamfered pixel button (global.css .btn). Orange primary by default; classes
///     <c>secondary</c> (violet), <c>ghost</c> (frame only), sizes <c>lg</c> and <c>sm</c>, <c>block</c> to stretch.
///     Its own type keeps Fluent's Button theme (rounded corners, hover greys) away; the look is in
///     Assets/Styles/HedgeFoxTheme.axaml.
/// </summary>
public class HfButton : Button
{
    protected override Type StyleKeyOverride => typeof(HfButton);
}

/// <summary>Square 32 px button of the custom title bar: «свернуть» and «закрыть» (class <c>close</c>).</summary>
public class HfCaptionButton : Button
{
    protected override Type StyleKeyOverride => typeof(HfCaptionButton);
}

/// <summary>
///     Small chamfered label (global.css .badge): Tiny5 16 px capitals in ink on brass by default; classes
///     <c>orange</c>, <c>purple</c>, <c>success</c>, <c>quiet</c>.
/// </summary>
public class HfBadge : ContentControl
{
    protected override Type StyleKeyOverride => typeof(HfBadge);
}
