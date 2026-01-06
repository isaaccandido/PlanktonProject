using Plankton.Core.Domain.ExceptionHandling;
using Plankton.Core.Enums;
using Plankton.Core.Interfaces;

namespace Plankton.Core.Domain.Commands.Infrastructure;

public sealed class TokenCommandAuthorizer(Dictionary<SourceType, string> tokens) : ICommandAuthorizer
{
    public Task AuthorizeAsync(CommandContext context)
    {
        var httpToken = tokens[SourceType.Http];
        var telegramToken = tokens[SourceType.Telegram];

        if (string.IsNullOrWhiteSpace(httpToken) && string.IsNullOrWhiteSpace(telegramToken)) return Task.CompletedTask;

        return context.Command.Source switch
        {
            SourceType.Http => !string.Equals(context.Token, httpToken, StringComparison.Ordinal)
                ? throw new UnauthorizedCommandException()
                : Task.CompletedTask,
            SourceType.Telegram => !string.Equals(context.Token, telegramToken, StringComparison.Ordinal)
                ? throw new UnauthorizedCommandException()
                : Task.CompletedTask,
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}