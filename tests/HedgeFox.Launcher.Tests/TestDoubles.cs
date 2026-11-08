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

/// <summary>An interface fake: the handler answers each call (by method name and arguments).</summary>
public class TestProxy : System.Reflection.DispatchProxy
{
    private Func<System.Reflection.MethodInfo, object?[]?, object?> _handler = (_, _) => null;
    public readonly List<string> Calls = [];

    public static T Create<T>(Func<System.Reflection.MethodInfo, object?[]?, object?> handler) where T : class
    {
        var proxy = Create<T, TestProxy>();
        ((TestProxy)(object)proxy)._handler = handler;
        return proxy;
    }

    public static TestProxy Of(object proxy) => (TestProxy)proxy;

    protected override object? Invoke(System.Reflection.MethodInfo? method, object?[]? args)
    {
        Calls.Add(method!.Name);
        return _handler(method, args);
    }
}
