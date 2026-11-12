using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Media;
using Avalonia.Styling;

namespace Gml.Launcher.Views.HedgeFox.Controls;

/// <summary>
///     How one section gives way to another (Играть, Новости, Настройки, Профиль, the login): the old page fades out
///     quickly, the new one fades in while it rises a few pixels into place. Nothing is held after the animations
///     (FillMode.None, as in Avalonia's CrossFade): the host swaps two presenters, and an opacity of 0 held on one of
///     them would leave the next page invisible when a quick second navigation cancels its fade-in.
/// </summary>
public sealed class HfPageTransition : IPageTransition
{
    public TimeSpan Duration { get; set; } = TimeSpan.FromMilliseconds(260);

    /// <summary>How far below its place the new page starts, in pixels.</summary>
    public double Rise { get; set; } = 14;

    public async Task Start(Visual? from, Visual? to, bool forward, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return;
        var running = new List<Task>();
        if (from is not null)
            running.Add(new Animation
            {
                Duration = TimeSpan.FromTicks(Duration.Ticks / 2),
                Easing = new CubicEaseIn(),
                FillMode = FillMode.None,
                Children =
                {
                    new KeyFrame { Cue = new Cue(0), Setters = { new Setter(Visual.OpacityProperty, 1d) } },
                    new KeyFrame { Cue = new Cue(1), Setters = { new Setter(Visual.OpacityProperty, 0d) } }
                }
            }.RunAsync(from, cancellationToken));
        if (to is not null)
        {
            to.IsVisible = true;
            running.Add(new Animation
            {
                Duration = Duration,
                Easing = new CubicEaseOut(),
                FillMode = FillMode.None,
                Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(0),
                        Setters = { new Setter(Visual.OpacityProperty, 0d), new Setter(TranslateTransform.YProperty, Rise) }
                    },
                    new KeyFrame
                    {
                        Cue = new Cue(1),
                        Setters = { new Setter(Visual.OpacityProperty, 1d), new Setter(TranslateTransform.YProperty, 0d) }
                    }
                }
            }.RunAsync(to, cancellationToken));
        }
        await Task.WhenAll(running);
        if (from is not null && !cancellationToken.IsCancellationRequested)
            from.IsVisible = false;
    }
}
