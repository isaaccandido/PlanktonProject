namespace Plankton.DataAccess.Interfaces;

public interface IInMemoryStore<T> : IDataStore<T> where T : class;