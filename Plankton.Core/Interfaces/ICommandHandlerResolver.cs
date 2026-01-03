namespace Plankton.Core.Interfaces;

public interface ICommandHandlerResolver
{
    ICommandHandler Resolve(string commandName);
}