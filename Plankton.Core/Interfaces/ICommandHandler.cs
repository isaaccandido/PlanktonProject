using Plankton.Core.Domain.Models;

namespace Plankton.Core.Interfaces;

public interface ICommandHandler
{
    string CommandName { get; }
    int MinArgs { get; }
    string[]? FixedArgs { get; }

    Task<object?> HandleAsync(CommandModel command);
}