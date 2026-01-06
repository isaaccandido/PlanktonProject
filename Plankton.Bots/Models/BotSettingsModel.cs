namespace Plankton.Bots.Models;

public class BotSettingsModel
{
    public bool Enabled { get; set; } = true;
    public TimeSpan? RunInterval { get; init; }
    public int MaxFailures { get; init; } = 3;
    public TimeSpan RestartDelay { get; init; } = TimeSpan.FromSeconds(5);
}