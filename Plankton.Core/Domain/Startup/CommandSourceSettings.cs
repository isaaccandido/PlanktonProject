namespace Plankton.Core.Domain.Startup;

public sealed class CommandSourceSettings
{
    public bool HttpEnabled { get; init; } = true;
    public bool TelegramEnabled { get; init; } = true;
}