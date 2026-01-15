using System.Collections.Concurrent;
using Plankton.DataAccess.Interfaces;

namespace Plankton.DataAccess.DataStores;

public sealed class InMemoryDataStore<T> : IDataStore<T> where T : class
{
    private readonly ConcurrentDictionary<string, T> _storage = new();

    public Task<T?> GetAsync(string key, CancellationToken ct = default)
    {
        _storage.TryGetValue(key, out var value);
        return Task.FromResult(value);
    }

    public Task SetAsync(string key, T entity, CancellationToken ct = default)
    {
        _storage[key] = entity;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        _storage.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}