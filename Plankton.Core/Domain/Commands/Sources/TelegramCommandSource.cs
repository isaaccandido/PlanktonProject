using Microsoft.Extensions.Logging;
using Plankton.Core.Domain.Commands.Infrastructure;
using Plankton.Core.Domain.Models;
using Plankton.Core.Interfaces;

namespace Plankton.Core.Domain.Commands.Sources;

public class TelegramCommandSource(ILogger<HttpCommandSource> logger) : ICommandSource
{
    public event Func<CommandContext, Task<object?>>? CommandReceived;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogWarning("Telegram is still in development...");

        CommandReceived?.Invoke(new CommandContext()
        {
            Command = new CommandModel
            {
                  Name =  "Telegram",
                  Args = []
            },
            CorrelationId =  Guid.NewGuid().ToString()
        });

        return Task.CompletedTask;
    }
}