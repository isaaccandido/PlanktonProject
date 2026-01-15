using Plankton.DataAccess.Interfaces;

namespace Plankton.DataAccess.DataStores;

public sealed class DatabaseDataStore<T> : IDataStore<T> where T : class
{
    public Task<T?> GetAsync(string key, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task SetAsync(string key, T entity, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}