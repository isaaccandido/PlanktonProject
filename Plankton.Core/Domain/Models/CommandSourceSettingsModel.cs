namespace Plankton.Core.Domain.Models;

public sealed class CommandSourceSettingsModel
{
    public bool HttpEnabled { get; init; } = true;
    public bool TelegramEnabled { get; init; } = true;
}