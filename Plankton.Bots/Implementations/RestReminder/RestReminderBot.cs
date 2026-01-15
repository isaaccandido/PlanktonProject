using Plankton.Bots.Interfaces;
using Plankton.Bots.Models;
using Plankton.DataAccess.Enums;

namespace Plankton.Bots.Implementations.RestReminder;

public class RestReminderBot : IBot
{
    public string Name => "RestReminder";
    public DataAccessType StateStorage => DataAccessType.InMemory;

    public BotSettingsModel Settings { get; set; } = new()
    {
        Enabled = true,
        RunInterval = TimeSpan.FromSeconds(5),
        MaxFailures = 3,
        RestartDelay = TimeSpan.FromSeconds(10)
    };


    public Task RunAsync(CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}