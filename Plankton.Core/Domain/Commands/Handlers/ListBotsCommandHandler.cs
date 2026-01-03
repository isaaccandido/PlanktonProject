using Microsoft.Extensions.Logging;
using Plankton.Core.Domain.Models;
using Plankton.Core.Enums;
using Plankton.Core.Interfaces;

namespace Plankton.Core.Domain.Commands.Handlers;

public sealed partial class ListBotsCommandHandler(ILogger<ListBotsCommandHandler> logger) : ICommandHandler
{
    public string CommandName => "list-bots";
    public string Description => "Lists all bots available.";
    public int MinArgs => 0;
    public string[]? FixedArgs => [];

    // For demo purposes, we'll just return a fixed list of bot names.
    // TODO make this real
    public Task<object?> HandleAsync(CommandModel command)
    {
        LogListingBots(logger, command.Source);

        var bots = new List<string>
        {
            "AlphaBot",
            "BetaBot",
            "GammaBot"
        };

        return Task.FromResult<object?>(bots);
    }

    [LoggerMessage(LogLevel.Information, "Listing bots (Source: {source})")]
    static partial void LogListingBots(ILogger<ListBotsCommandHandler> logger, SourceType? source);
}