using Microsoft.Extensions.Options;

namespace EmailPagamenti.Tests;

/// <summary>IOptionsMonitor minimo: evita di tirarsi dietro una libreria di mock per due test.</summary>
internal sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T>
{
    public StaticOptionsMonitor(T value) => CurrentValue = value;

    public T CurrentValue { get; }

    public T Get(string? name) => CurrentValue;

    public IDisposable? OnChange(Action<T, string?> listener) => null;
}
