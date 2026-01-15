using Microsoft.Extensions.Logging;
using Plankton.Bots;
using Plankton.Bots.Models;
using Plankton.Core.Domain.Models;
using Plankton.Core.Enums;
using Plankton.Core.Interfaces;

namespace Plankton.Core.Domain.Commands.Handlers;

public sealed partial class BotCommandHandler(
    ILogger<BotCommandHandler> logger,
    BotEngine botEngine
) : ICommandHandler
{
    public string CommandName => "bot";

    public string Description => """
                                 Centralized bot command handler. Supports both controlling bots and retrieving status.

                                 Actions supported:
                                 - start <botName>   : Starts the bot if idle.
                                 - stop <botName>    : Stops the bot if running.
                                 - restart <botName> : Restarts the bot and resets crash count.
                                 - enable <botName>  : Enables a disabled bot.
                                 - disable <botName> : Disables the bot immediately.
                                 - status <botName>  : Retrieves the full status of the bot.
                                 - full-report       : Retrieves the full status of all bots.
                                 - reset <botName>   : Resets the bot.

                                 Examples:
                                 bot start MyBot
                                 bot stop MyBot
                                 bot restart MyBot
                                 bot enable MyBot
                                 bot disable MyBot
                                 bot status MyBot
                                 bot status MyBot
                                 """;

    public int MinArgs => 2;
    public string[] FixedArgs => [];

    public Task<object?> HandleAsync(CommandModel command)
    {
        if (command.Args.Count < 2) return Task.FromResult<object?>("Usage: bot <action> <botName>");

        var action = command.Args[0].ToLowerInvariant();
        var botName = command.Args[1];

        if (action == "status")
        {
            var botStatus = botEngine.GetBotStatus(botName);

            LogBotStatus(logger, command.Source, botName);

            if (botStatus is null) return Task.FromResult<object?>($"Bot '{botName}' not found.");

            return Task.FromResult<object?>(new
            {
                botStatus.Name,
                botStatus.Status,
                botStatus.CrashCount,
                botStatus.IsRunning,
                botStatus.Settings,
                botStatus.NextRun,
                botStatus.Reason
            });
        }

        var result = action switch
        {
            "start" => botEngine.StartBot(botName),
            "stop" => botEngine.StopBot(botName),
            "restart" => botEngine.RestartBot(botName),
            "enable" => botEngine.EnableBot(botName),
            "disable" => botEngine.DisableBot(botName),
            "reset" => botEngine.ResetBotState(botName),
            "full-report" => botEngine.GetAllBotStatuses(),
            _ => new BotActionResultModel(
                false,
                $"Unknown action. Run 'list-commands' to get available parameters for '{CommandName}'."
            )
        };

        LogBotCommand(logger, command.Source, action, botName, result.Success);

        return Task.FromResult<object?>(result);
    }

    [LoggerMessage(LogLevel.Information, "Status requested for {botName}, (Source: {source})")]
    static partial void LogBotStatus(
        ILogger<BotCommandHandler> logger,
        SourceType? source,
        string botName
    );

    [LoggerMessage(
        LogLevel.Information,
        "Bot command received: Action={action}, Bot={botName}, Success={success} (Source: {source})"
    )]
    static partial void LogBotCommand(
        ILogger<BotCommandHandler> logger,
        SourceType? source,
        string action,
        string botName,
        bool success
    );
}