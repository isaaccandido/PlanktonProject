using Microsoft.Extensions.DependencyInjection;
using Plankton.DataAccess.Enums;
using Plankton.DataAccess.Interfaces;

namespace Plankton.DataAccess;

public sealed class DataAccessEngine(IServiceProvider serviceProvider)
{
    public IDataStore<T> Resolve<T>(DataAccessType type) where T : class
    {
        return type switch
        {
            DataAccessType.InMemory => serviceProvider.GetRequiredService<IDataStore<T>>(),
            DataAccessType.Database => serviceProvider.GetRequiredService<IDataStore<T>>(),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }
}