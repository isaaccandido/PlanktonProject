using Plankton.Bots.Models;
using Plankton.Bots.Utils;

namespace Plankton.Bots.Interfaces;

public interface IBot
{
    string Name { get; }
    BotWebTools BotWebTools { get; }
    BotSettingsModel Settings { get; }

    Task RunAsync(CancellationToken ct);
}