using Plankton.Bots.Enums;

namespace Plankton.Bots.Models;

public class BotStatusModel
{
    public string Name { get; init; } = null!;
    public BotStatus Status { get; init; }
    public int CrashCount { get; init; }
    public BotSettingsModel Settings { get; init; } = null!;
    public bool IsRunning { get; init; }
    public DateTime? NextRun { get; init; }
    public string? Reason { get; init; }
}