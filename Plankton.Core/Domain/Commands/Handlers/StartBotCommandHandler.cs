using Microsoft.Extensions.Logging;
using Plankton.Core.Domain.ExceptionHandling;
using Plankton.Core.Domain.Models;
using Plankton.Core.Enums;
using Plankton.Core.Interfaces;

namespace Plankton.Core.Domain.Commands.Handlers;

public sealed partial class StartBotCommandHandler(ILogger<StartBotCommandHandler> logger) : ICommandHandler
{
    public string CommandName => "start";

    public Task<object?> HandleAsync(CommandModel command)
    {
        var botName = command.Args.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(botName)) throw new InvalidCommandException("Bot name must be provided");

        LogStartingBotBotNameSourceSource(logger, botName, command.Source);

        // Return null because "start" does not produce a value
        return Task.FromResult<object?>(null);
    }

    [LoggerMessage(LogLevel.Information, "Starting bot '{botName}' (Source: {source})")]
    static partial void LogStartingBotBotNameSourceSource(
        ILogger<StartBotCommandHandler> logger,
        string botName,
        SourceType? source
    );
}