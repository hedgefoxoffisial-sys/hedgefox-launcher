using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Gml.Launcher.Core.HedgeFox;

namespace Gml.Launcher.Views.HedgeFox.Controls;

/// <summary>
///     The stock toast fill (a hex the stock view model passes) → the HedgeFox toast's left bar: HfError, HfSuccess
///     or HfPurple (<see cref="HfToastTones" />). The fill itself is never painted.
/// </summary>
public sealed class HfToastAccent : IValueConverter
{
    public static readonly HfToastAccent Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var color = value switch
        {
            ISolidColorBrush brush => brush.Color,
            Color c => c,
            _ => (Color?)null
        };
        var key = HfToastTones.For(color) switch
        {
            HfToastTone.Error => "HfErrorBrush",
            HfToastTone.Success => "HfSuccessBrush",
            _ => "HfPurpleBrush"
        };
        var app = Application.Current;
        return app is not null && app.TryFindResource(key, app.ActualThemeVariant, out var found) ? found : null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
