using Plankton.Core.Domain.Commands.Infrastructure;

namespace Plankton.Core.Interfaces;

public interface ICommandSource
{
    event Func<CommandContext, Task<object?>>? CommandReceived;
    Task StartAsync(CancellationToken cancellationToken);
}