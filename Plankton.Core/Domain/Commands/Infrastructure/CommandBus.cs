using Plankton.Core.Domain.ExceptionHandling;
using Plankton.Core.Interfaces;

namespace Plankton.Core.Domain.Commands.Infrastructure;

public sealed class CommandBus(
    IEnumerable<ICommandValidator> validators,
    ICommandAuthorizer authorizer,
    CommandRateLimiter rateLimiter,
    ICommandHandlerResolver resolver)
{
    public async Task<object?> DispatchAsync(CommandContext context)
    {
        if (context.Command is null) throw new InvalidCommandException("Command is missing");

        foreach (var validator in validators) await validator.ValidateAsync(context.Command);

        await authorizer.AuthorizeAsync(context);

        object? result = null;

        await rateLimiter.ExecuteAsync(async () =>
        {
            var handler = resolver.Resolve(context.Command.Name);
            result = await handler.HandleAsync(context.Command);
        });

        return result;
    }
}