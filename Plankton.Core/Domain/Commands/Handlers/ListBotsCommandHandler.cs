using Microsoft.Extensions.Logging;
using Plankton.Core.Domain.Models;
using Plankton.Core.Enums;
using Plankton.Core.Interfaces;

namespace Plankton.Core.Domain.Commands.Handlers;

public sealed partial class ListBotsCommandHandler(ILogger<ListBotsCommandHandler> logger) : ICommandHandler
{
    public string CommandName => "list";

    // For demo purposes, we'll just return a fixed list of bot names.
    // You can replace this with your real bot manager/service.
    public Task<object?> HandleAsync(CommandModel command)
    {
        LogListingBots(logger, command.Source);

        var bots = new List<string>
        {
            "AlphaBot",
            "BetaBot",
            "GammaBot"
        };

        // Return the list as object
        return Task.FromResult<object?>(bots);
    }

    [LoggerMessage(LogLevel.Information, "Listing bots (Source: {source})")]
    static partial void LogListingBots(ILogger<ListBotsCommandHandler> logger, SourceType? source);
}