namespace Plankton.Bots.Models;

public class BotsHttpSettings
{
    public BotHttpSettings Default { get; set; } = new();
    public Dictionary<string, BotHttpSettings> Bots { get; set; } = new();
}