using Gml.Launcher.Core.Services;

namespace HedgeFox.Launcher.Tests;

/// <summary>The launcher's key-value storage in memory.</summary>
internal sealed class TestStorage : IStorageService
{
    private readonly Dictionary<string, object?> _values = new();

    public Task SetAsync<T>(string key, T value, CancellationToken? token = default)
    {
        _values[key] = value;
        return Task.CompletedTask;
    }

    public Task<T?> GetAsync<T>(string key) =>
        Task.FromResult(_values.TryGetValue(key, out var v) && v is T t ? t : default);

    public Task<int> SaveRecord<T>(T value) => Task.FromResult(0);
    public Task<string> GetLogsAsync(int rowCount) => Task.FromResult(string.Empty);
}
