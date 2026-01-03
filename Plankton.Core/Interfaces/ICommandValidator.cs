using Plankton.Core.Domain.Models;

namespace Plankton.Core.Interfaces;

public interface ICommandValidator
{
    Task ValidateAsync(CommandModel command);
}