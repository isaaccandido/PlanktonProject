using Plankton.Core.Domain.ExceptionHandling;
using Plankton.Core.Domain.Models;
using Plankton.Core.Interfaces;

namespace Plankton.Core.Domain.Commands.Validation;

public sealed class BasicCommandValidator : ICommandValidator
{
    public Task ValidateAsync(CommandModel command)
    {
        return string.IsNullOrWhiteSpace(command.Name)
            ? throw new InvalidCommandException("Command name is required")
            : Task.CompletedTask;
    }
}