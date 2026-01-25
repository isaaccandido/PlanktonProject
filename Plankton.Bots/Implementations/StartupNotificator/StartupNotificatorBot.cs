using Plankton.Bots.Interfaces;
using Plankton.Bots.Models;
using Plankton.Bots.Utils;
using Plankton.DataAccess.Enums;

namespace Plankton.Bots.Implementations.StartupNotificator;

public class StartupNotificatorBot(BotWebTools botWebTools) : IBot
{
    public string Name => "StartupNotificator";
    public BotSettingsModel Settings { get; set; } = new()
    {
        Enabled = true,
        RunInterval = TimeSpan.FromDays(1),
        MaxFailures = 3,
        RestartDelay = TimeSpan.FromSeconds(10)
    };
    public DataAccessType StateStorage => DataAccessType.InMemory;
    private static string NotificationUrl => "https://ntfy.sh/Godofredo";
    
    public async Task RunAsync(CancellationToken ct)
    {
        const string message = "Plankton engine is live.";

        await botWebTools.SendAsync<object>(HttpMethod.Post, botId: Name, NotificationUrl, body: message, ct: ct);
    }
}