using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;

namespace Gml.Launcher.Core.HedgeFox;

public enum HfAppMenuAction
{
    Hide,
    Quit
}

public sealed record HfAppMenuItem(string Header, string Gesture, HfAppMenuAction Action);

/// <summary>
///     The macOS app menu (the bold «HedgeFox» in the menu bar). Without one, Avalonia builds its own: «About
///     Avalonia» (its English about box), «Services», «Hide Application», «Quit». This one is Russian and has what a
///     player needs: hide (⌘H) and quit (⌘Q). <see cref="HedgeFoxServices" /> turns Avalonia's items off and sets this
///     menu before the native menu is built.
/// </summary>
public static class HfAppMenu
{
    public static IReadOnlyList<HfAppMenuItem> Items { get; } =
    [
        new("Скрыть HedgeFox", "Cmd+H", HfAppMenuAction.Hide),
        new("Выйти из HedgeFox", "Cmd+Q", HfAppMenuAction.Quit)
    ];

    /// <summary>Names the app and gives it this menu; runs after the app object exists, before the native menu.</summary>
    public static void Apply(Application app)
    {
        app.Name = HfStrings.Name;
        var menu = new NativeMenu();
        foreach (var item in Items)
        {
            if (item.Action == HfAppMenuAction.Quit)
                menu.Add(new NativeMenuItemSeparator());
            var native = new NativeMenuItem(item.Header) { Gesture = KeyGesture.Parse(item.Gesture) };
            var action = item.Action;
            native.Click += (_, _) => Run(action);
            menu.Add(native);
        }

        NativeMenu.SetMenu(app, menu);
    }

    private static void Run(HfAppMenuAction action)
    {
        var app = Application.Current;
        switch (action)
        {
            case HfAppMenuAction.Hide:
                (app?.TryGetFeature(typeof(IActivatableLifetime)) as IActivatableLifetime)?.TryEnterBackground();
                break;
            case HfAppMenuAction.Quit:
                // as Avalonia's own «Quit»: a classic desktop app closes its windows first
                if (app?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    desktop.TryShutdown();
                else if (app?.ApplicationLifetime is IControlledApplicationLifetime controlled)
                    controlled.Shutdown();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action));
        }
    }
}
