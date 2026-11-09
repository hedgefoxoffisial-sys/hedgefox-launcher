using System.Threading.Tasks;
using Gml.Launcher.Core.HedgeFox;
using Gml.Launcher.Core.Services;

namespace Gml.Launcher.ViewModels.HedgeFox;

/// <summary>
///     The player's own pick of the version of the game (<see cref="HfProfilePick" />), in memory as well as in
///     storage: the main screen reads it the moment Настройки wrote it, before the storage write has finished.
/// </summary>
public static class HfVersionChoice
{
    private static readonly object Gate = new();
    private static bool _known;
    private static string? _value;

    public static async Task<string?> GetAsync(IStorageService? storage)
    {
        lock (Gate)
            if (_known || storage is null)
                return _value;
        var stored = await storage.GetAsync<string>(HfProfilePick.StorageKey);
        lock (Gate)
        {
            if (!_known)
            {
                _value = stored;
                _known = true;
            }
            return _value;
        }
    }

    /// <summary>The pick as known in memory now (after <see cref="GetAsync" /> or <see cref="SetAsync" />).</summary>
    public static string? Peek()
    {
        lock (Gate)
            return _value;
    }

    /// <summary>Remembers <paramref name="value" /> (a GML name, or null: the launcher picks for this PC).</summary>
    public static Task SetAsync(IStorageService? storage, string? value)
    {
        lock (Gate)
        {
            _value = value;
            _known = true;
        }
        return storage?.SetAsync(HfProfilePick.StorageKey, value) ?? Task.CompletedTask;
    }
}
