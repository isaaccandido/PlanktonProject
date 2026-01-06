using Microsoft.Extensions.Logging;
using Plankton.Core.Domain.ExceptionHandling;
using Plankton.Core.Interfaces;

namespace Plankton.Core.Domain.Commands.Infrastructure;

public sealed class CommandHandlerResolver : ICommandHandlerResolver
{
    private readonly IReadOnlyDictionary<string, ICommandHandler> _handlers;

    public CommandHandlerResolver(
        ILogger<CommandHandlerResolver> logger,
        IEnumerable<ICommandHandler>? handlers)
    {
        var commandHandlers = handlers?.ToArray() ?? Array.Empty<ICommandHandler>();

        if (commandHandlers.Length == 0) logger.LogWarning("No handlers were found.");

        _handlers = commandHandlers.ToDictionary(
            h => h.CommandName,
            StringComparer.OrdinalIgnoreCase
        );
    }

    public ICommandHandler Resolve(string commandName)
    {
        return !_handlers.TryGetValue(commandName, out var handler)
            ? throw new InvalidCommandException($"Unknown command '{commandName}'")
            : handler;
    }
}