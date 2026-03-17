using Plankton.Bots.Interfaces;
using Plankton.Bots.Models;
using Plankton.Bots.Utils;
using Plankton.DataAccess.Enums;

namespace Plankton.Bots.Implementations.StartupNotificator;

public class StartupNotificatorBot(BotWebTools botWebTools) : IBot
{
    private static string NotificationUrl => "https://ntfy.sh/Godofredo";
    public string Name => "StartupNotificator";

    public BotSettingsModel Settings { get; set; } = new()
    {
        Enabled = true,
        RunInterval = TimeSpan.FromDays(1),
        MaxFailures = 3,
        RestartDelay = TimeSpan.FromSeconds(10)
    };

    public DataAccessType StateStorage => DataAccessType.InMemory;

    public async Task RunAsync(CancellationToken ct)
    {
        const string message = "Plankton engine is live.";

        await botWebTools.SendAsync<object>(HttpMethod.Post, Name, NotificationUrl, message, ct);
    }
}