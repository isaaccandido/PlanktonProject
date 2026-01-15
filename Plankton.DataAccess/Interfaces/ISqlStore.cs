namespace Plankton.DataAccess.Interfaces;

public interface ISqlStore<T> : IDataStore<T> where T : class;