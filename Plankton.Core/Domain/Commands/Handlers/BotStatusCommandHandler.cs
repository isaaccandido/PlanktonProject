using Microsoft.Extensions.Logging;
using Plankton.Core.Domain.ExceptionHandling;
using Plankton.Core.Domain.Models;
using Plankton.Core.Interfaces;

namespace Plankton.Core.Domain.Commands.Handlers;

public sealed class BotStatusCommandHandler(ILogger<BotStatusCommandHandler> logger) : ICommandHandler
{
    public string CommandName => "bot-status";
    public string Description => "Starts a bot or multiple bots by name.";
    public int MinArgs => 1;
    public string[]? FixedArgs => [];

    public Task<object?> HandleAsync(CommandModel command)
    {
        if (command.Args is null || command.Args.Count == 0)
            throw new InvalidCommandException("Bot name must be provided");

        var botNames = command.Args
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Select(a => a.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();

        if (botNames.Count == 0) throw new InvalidCommandException("Bot name must be provided");

        logger.LogInformation("Attempting to get bot status for the following bots: {}", string.Join(", ", botNames));

        return Task.FromResult<object?>(null);
    }
}