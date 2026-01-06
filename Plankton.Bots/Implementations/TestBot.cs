using Microsoft.Extensions.Logging;
using Plankton.Bots.Interfaces;
using Plankton.Bots.Models;
using Plankton.Bots.Utils;

namespace Plankton.Bots.Implementations;

public class TestBot(ILogger<TestBot> logger, BotWebTools botWebTools) : IBot
{
    public BotWebTools BotWebTools { get; } = botWebTools;

    public BotSettingsModel Settings { get; set; } = new()
    {
        Enabled = true,
        RunInterval = TimeSpan.FromSeconds(5),
        MaxFailures = 3,
        RestartDelay = TimeSpan.FromSeconds(10)
    };

    public string Name { get; init; } = "TestBot";

    public bool IsRunning { get; set; }

    public Task RunAsync(CancellationToken ct)
    {
        logger.LogInformation("Testing bot is running...");
        Thread.Sleep(5000);
        return Task.CompletedTask;
    }
}