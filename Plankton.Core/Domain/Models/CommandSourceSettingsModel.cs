namespace Plankton.Core.Domain.Models;

public sealed class CommandSourceSettingsModel
{
    public Http Http { get; init; } = new();
    public Telegram Telegram { get; init; } = new();
}

public sealed class Http
{
    public bool Enabled { get; init; } = true;
}

public sealed class Telegram
{
    public bool Enabled { get; init; } = true;
}