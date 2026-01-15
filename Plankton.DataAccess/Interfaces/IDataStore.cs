namespace Plankton.DataAccess.Interfaces;

public interface IDataStore<T> where T : class
{
    Task<T?> GetAsync(string key, CancellationToken ct = default);
    Task SetAsync(string key, T entity, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
}