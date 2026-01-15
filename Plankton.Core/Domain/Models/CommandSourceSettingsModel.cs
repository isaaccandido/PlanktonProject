namespace Plankton.Core.Domain.Models;

public sealed class CommandSourceSettingsModel
{
    public Http Http { get; init; } = new();
    public Telegram Telegram { get; init; } = new();
}