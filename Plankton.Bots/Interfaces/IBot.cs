using Plankton.Bots.Models;
using Plankton.DataAccess.Enums;

namespace Plankton.Bots.Interfaces;

public interface IBot
{
    string Name { get; }
    BotSettingsModel Settings { get; set; }
    DataAccessType StateStorage { get; }

    Task RunAsync(CancellationToken ct);
}