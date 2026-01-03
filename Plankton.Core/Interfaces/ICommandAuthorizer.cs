using Plankton.Core.Domain.Commands.Infrastructure;

namespace Plankton.Core.Interfaces;

public interface ICommandAuthorizer
{
    Task AuthorizeAsync(CommandContext context);
}