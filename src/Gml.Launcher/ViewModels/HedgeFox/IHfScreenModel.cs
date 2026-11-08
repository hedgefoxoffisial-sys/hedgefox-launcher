using System;

namespace Gml.Launcher.ViewModels.HedgeFox;

/// <summary>A HedgeFox screen's own state next to a stock page view model; it runs (polls, loads) while shown.</summary>
public interface IHfScreenModel
{
    /// <summary>Starts polling and loading; dispose the result when the screen goes away.</summary>
    IDisposable Start();
}
