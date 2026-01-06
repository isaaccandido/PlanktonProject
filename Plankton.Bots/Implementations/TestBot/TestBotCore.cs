using System.Text.Json;
using Microsoft.Extensions.Logging;
using Plankton.Bots.Interfaces;
using Plankton.Bots.Models;
using Plankton.Bots.Utils;

namespace Plankton.Bots.Implementations.TestBot;

public class TestBotCore(ILogger<TestBotCore> logger, BotWebTools botWebTools) : IBot
{
    public BotSettingsModel Settings { get; set; } = new()
    {
        Enabled = true,
        RunInterval = TimeSpan.FromSeconds(5),
        MaxFailures = 3,
        RestartDelay = TimeSpan.FromSeconds(10)
    };

    public string Name { get; init; } = "TestBot";

    public Task RunAsync(CancellationToken ct)
    {
        var result = botWebTools.SendAsync<Dictionary<string, object>>(
            HttpMethod.Get,
            Name,
            "https://api.adviceslip.com/advice",
            null,
            ct
        ).Result;

        logger.LogInformation(JsonSerializer.Serialize(result));

        return Task.CompletedTask;
    }
}