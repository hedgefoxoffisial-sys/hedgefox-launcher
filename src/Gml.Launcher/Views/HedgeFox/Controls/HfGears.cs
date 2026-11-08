using System;
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Gml.Launcher.Core.HedgeFox;

namespace Gml.Launcher.Views.HedgeFox.Controls;

/// <summary>
///     The loader's three meshing pixel cogs (<see cref="HfCogs.Loader" />, the site's PixelGears.astro): each cog is
///     drawn as rows of cells with aliased edges, so it reads as a voxel model while it turns, with its dark
///     extruded side one cell down and right. <see cref="Cell" /> px per cell (3 → 192×189). Turning runs at
///     30 frames a second while the control is shown; <see cref="IsSpinning" /> = false holds the first frame.
/// </summary>
public class HfGears : Control
{
    public static readonly StyledProperty<double> CellProperty =
        AvaloniaProperty.Register<HfGears, double>(nameof(Cell), 3);

    public static readonly StyledProperty<bool> IsSpinningProperty =
        AvaloniaProperty.Register<HfGears, bool>(nameof(IsSpinning), true);

    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(33);
    private static readonly (int MinX, int MinY, int Width, int Height) Box = HfCogs.Bounds(HfCogs.Loader);
    private static Layer[]? _layers;

    private readonly Stopwatch _clock = new();
    private DispatcherTimer? _timer;

    static HfGears()
    {
        AffectsMeasure<HfGears>(CellProperty);
        AffectsRender<HfGears>(CellProperty);
    }

    public HfGears()
    {
        IsHitTestVisible = false;
        RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
    }

    public double Cell
    {
        get => GetValue(CellProperty);
        set => SetValue(CellProperty, value);
    }

    public bool IsSpinning
    {
        get => GetValue(IsSpinningProperty);
        set => SetValue(IsSpinningProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) => new(Box.Width * Cell, Box.Height * Cell);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSpinningProperty || change.Property == IsVisibleProperty)
            UpdateTimer();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateTimer();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        UpdateTimer();
    }

    private void UpdateTimer()
    {
        var run = IsSpinning && IsVisible && this.GetVisualRoot() != null;
        if (run && _timer is null)
        {
            _clock.Start();
            _timer = new DispatcherTimer(Frame, DispatcherPriority.Background, (_, _) => InvalidateVisual());
            _timer.Start();
        }
        else if (!run && _timer is not null)
        {
            _timer.Stop();
            _timer = null;
            _clock.Stop();
        }
    }

    public override void Render(DrawingContext context)
    {
        var seconds = IsSpinning ? _clock.Elapsed.TotalSeconds : 0;
        var cell = Cell;
        foreach (var layer in Layers)
        {
            var cog = layer.Cog;
            var half = HfCogs.SizeOf(cog.Options) / 2.0;
            var angle = HfCogs.Angle(cog, seconds) * Math.PI / 180;
            var spin = Matrix.CreateTranslation(-half, -half) * Matrix.CreateRotation(angle);
            var at = Matrix.CreateTranslation(cog.X - Box.MinX, cog.Y - Box.MinY) * Matrix.CreateScale(cell, cell);
            // the side first, one cell right and one and a half down, then the face over it
            using (context.PushTransform(spin * Matrix.CreateTranslation(1, 1.5) * at))
                context.DrawGeometry(layer.Side, null, layer.Silhouette);
            using (context.PushTransform(spin * at))
                foreach (var (brush, geometry) in layer.Faces)
                    context.DrawGeometry(brush, null, geometry);
        }
    }

    private static Layer[] Layers => _layers ??= Build();

    private static Layer[] Build()
    {
        var layers = new List<Layer>();
        foreach (var cog in HfCogs.Loader)
        {
            var grid = PixelGear.Raster(cog.Options);
            layers.Add(new Layer(cog,
                Brush(cog.Side), Runs(grid, t => t != GearTone.None),
                [
                    (Brush(cog.Body), Runs(grid, t => t == GearTone.Body)),
                    (Brush(cog.Tip), Runs(grid, t => t == GearTone.Tip)),
                    (Brush(cog.Hub), Runs(grid, t => t == GearTone.Hub)),
                    (Brush(cog.Shaft), Runs(grid, t => t == GearTone.Shaft))
                ]));
        }

        return layers.ToArray();
    }

    private static IBrush Brush(string hex) => new ImmutableSolidColorBrush(Color.Parse(hex));

    /// <summary>Rows of equal cells merged into rectangles, like the site's path data.</summary>
    private static Geometry Runs(GearTone[,] grid, Func<GearTone, bool> want)
    {
        var g = new StreamGeometry();
        using var c = g.Open();
        for (var y = 0; y < grid.GetLength(0); y++)
        {
            var x = 0;
            while (x < grid.GetLength(1))
            {
                if (!want(grid[y, x]))
                {
                    x++;
                    continue;
                }

                var end = x;
                while (end < grid.GetLength(1) && want(grid[y, end]))
                    end++;
                c.BeginFigure(new Point(x, y), true);
                c.LineTo(new Point(end, y));
                c.LineTo(new Point(end, y + 1));
                c.LineTo(new Point(x, y + 1));
                c.EndFigure(true);
                x = end;
            }
        }

        return g;
    }

    private sealed record Layer(HfCog Cog, IBrush Side, Geometry Silhouette, (IBrush Brush, Geometry Geometry)[] Faces);
}
