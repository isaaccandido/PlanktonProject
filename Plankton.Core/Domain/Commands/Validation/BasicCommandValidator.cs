using Plankton.Core.Domain.ExceptionHandling;
using Plankton.Core.Domain.Models;
using Plankton.Core.Interfaces;

namespace Plankton.Core.Domain.Commands.Validation;

public sealed class BasicCommandValidator(ICommandHandlerResolver resolver) : ICommandValidator
{
    private readonly ICommandHandlerResolver _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));

    public Task ValidateAsync(CommandModel command)
    {
        if (string.IsNullOrWhiteSpace(command.Name)) throw new InvalidCommandException("Command name is required");

        var handler = _resolver.Resolve(command.Name);

        var minArgs = handler.MinArgs;
        var fixedArgs = handler.FixedArgs?.Where(arg => !string.IsNullOrWhiteSpace(arg)).ToArray();

        if ((command.Args?.Count ?? 0) < minArgs)
        {
            var message = $"Command '{command.Name}' requires at least {minArgs} argument(s).";
            if (fixedArgs?.Length > 0) message += $" Allowed: [{string.Join(", ", fixedArgs)}]";

            throw new InvalidCommandException(message, fixedArgs);
        }

        if (!(fixedArgs?.Length > 0)) return Task.CompletedTask;

        var invalidArg = command.Args?.FirstOrDefault(arg =>
        {
            return !fixedArgs.Any(f => string.Equals(f, arg, StringComparison.OrdinalIgnoreCase));
        });

        if (invalidArg == null) return Task.CompletedTask;
        
        throw new InvalidCommandException(
            $"Invalid argument '{invalidArg}' for command '{command.Name}'. " +
            $"Allowed: {string.Join(", ", fixedArgs)}",
            fixedArgs);
    }
}