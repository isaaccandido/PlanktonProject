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

        var minArgs = handler.GetType().GetProperty("MinArgs")?.GetValue(handler) as int? ?? 0;

        if ((command.Args?.Count ?? 0) < minArgs)
        {
            var allowedMsg = GetFixedArgsMessage(handler);
            throw new InvalidCommandException(
                $"Command '{command.Name}' requires at least {minArgs} argument(s). {allowedMsg}".Trim()
            );
        }

        if (handler.GetType().GetProperty("FixedArgs")?.GetValue(handler)
                is not string[] { Length: > 0 } fixedArgs || command.Args == null)
            return Task.CompletedTask;

        var invalidArg = command.Args.FirstOrDefault(arg =>
        {
            return !fixedArgs.Any(f => string.Equals(f, arg, StringComparison.OrdinalIgnoreCase));
        });

        if (invalidArg != null)
            throw new InvalidCommandException(
                $"Invalid argument '{invalidArg}' for command '{command.Name}'. " +
                $"Allowed arguments: {string.Join(", ", fixedArgs)}"
            );

        return Task.CompletedTask;
    }


    private static string GetFixedArgsMessage(ICommandHandler handler)
    {
        if (handler.GetType().GetProperty("FixedArgs")?.GetValue(handler) is string[] fixedArgs &&
            fixedArgs.Length > 0)
            return $"Allowed arguments: [{string.Join(", ", fixedArgs)}].";

        return string.Empty;
    }
}