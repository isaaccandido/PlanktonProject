using Microsoft.Extensions.Logging;
using Plankton.Core.Domain.Models;
using Plankton.Core.Enums;
using Plankton.Core.Interfaces;

namespace Plankton.Core.Domain.Commands.Handlers;

public sealed partial class ShutdownSuiteCommandHandler(
    Func<PlanktonHostEngine> engineFactory,
    ILogger<ShutdownSuiteCommandHandler> logger
) : ICommandHandler
{
    private readonly Func<PlanktonHostEngine> _engineFactory =
        engineFactory ?? throw new ArgumentNullException(nameof(engineFactory));

    public string CommandName => "shutdown-suite";

    public string? Description => """
                                  Stops the entire Plankton suite immediately.

                                  This is equivalent to an emergency stop — use with caution.
                                  All running bots and processes will be halted.

                                  Example usage:
                                  shutdown-suite
                                  """;

    public int MinArgs => 0;
    public string[] FixedArgs => [];

    public async Task<object?> HandleAsync(CommandModel command)
    {
        LogShutdownSuiteCommandInvokedSourceSource(logger, command.Source);

        var engine = _engineFactory();
        await engine.ShutdownAsync();

        return null;
    }

    [LoggerMessage(LogLevel.Information, "Shutdown-suite command invoked (Source: {source})")]
    static partial void LogShutdownSuiteCommandInvokedSourceSource(
        ILogger<ShutdownSuiteCommandHandler> logger,
        SourceType? source
    );
}