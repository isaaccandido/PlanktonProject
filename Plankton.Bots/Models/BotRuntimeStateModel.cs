using Plankton.Bots.Enums;

namespace Plankton.Bots.Models;

public sealed class BotRuntimeStateModel
{
    public string BotName { get; init; } = null!;
    public BotStatus Status { get; set; }
    public int CrashCount { get; set; }
    public DateTime? NextRunUtc { get; set; }
}