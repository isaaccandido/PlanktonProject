using Plankton.Core.Domain.ExceptionHandling;
using Plankton.Core.Interfaces;

namespace Plankton.Core.Domain.Commands.Infrastructure;

public sealed class TokenCommandAuthorizer(string adminToken) : ICommandAuthorizer
{
    public Task AuthorizeAsync(CommandContext context)
    {
        if (string.IsNullOrWhiteSpace(adminToken)) return Task.CompletedTask;

        return !string.Equals(context.Token, adminToken, StringComparison.Ordinal)
            ? throw new UnauthorizedCommandException()
            : Task.CompletedTask;
    }
}