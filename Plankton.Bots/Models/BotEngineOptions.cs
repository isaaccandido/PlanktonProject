using Plankton.DataAccess.Enums;

namespace Plankton.Bots.Models;

public sealed class BotEngineOptions
{
    public DataAccessType RuntimeStateStorage { get; init; } = DataAccessType.InMemory;
}