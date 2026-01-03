using Microsoft.Extensions.Logging;
using Plankton.Core.Domain.Commands.Infrastructure;
using Plankton.Core.Domain.Models;
using Plankton.Core.Interfaces;

namespace Plankton.Core;

public partial class Engine(ILogger<Engine> logger, CommandBus commandBus)
{
    public required CliArgsResultModel CliArgs { get; set; } // TODO find an use for CLI args

    private readonly List<ICommandSource> _commandSources = [];
    private readonly CancellationTokenSource _cts = new();

    public void RegisterCommandSource(ICommandSource source)
    {
        LogRegisteringCommandSourceSource(
            logger,
            source.GetType().Name
        );

        source.CommandReceived += HandleCommandAsync;
        _commandSources.Add(source);
    }

    public async Task RunAsync()
    {
        LogEngineStartedWaitingForCommands(logger);

        LogRegisteredSourceCountCommandSourceSSources(
            logger,
            _commandSources.Count,
            _commandSources.Select(s => s.GetType().Name)
        );

        foreach (var source in _commandSources)
        {
            LogStartingCommandSourceSource(
                logger,
                source.GetType().Name
            );

            _ = source.StartAsync(_cts.Token);
        }

        try
        {
            await Task.Delay(Timeout.Infinite, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            // expected on shutdown
        }

        LogEngineStopped(logger);
    }

    private async Task<object?> HandleCommandAsync(CommandContext context)
    {
        using var scope = logger.BeginScope(
            "CorrelationId:{CorrelationId}",
            context.CorrelationId
        );

        LogReceivedCommandCommand(logger, context.Command.Name);

        return await commandBus.DispatchAsync(context);
    }

    public async Task ShutdownAsync()
    {
        logger.LogInformation("Shutdown requested");

        foreach (var source in _commandSources) source.CommandReceived -= HandleCommandAsync;

        await _cts.CancelAsync();
        await Task.CompletedTask;
    }

    [LoggerMessage(LogLevel.Information, "Engine started. Waiting for commands.")]
    static partial void LogEngineStartedWaitingForCommands(ILogger<Engine> logger);

    [LoggerMessage(LogLevel.Information, "Engine stopped.")]
    static partial void LogEngineStopped(ILogger<Engine> logger);

    [LoggerMessage(LogLevel.Information, "Received command: {command}")]
    static partial void LogReceivedCommandCommand(ILogger<Engine> logger, string command);

    [LoggerMessage(LogLevel.Information, "Registering command source {source}")]
    static partial void LogRegisteringCommandSourceSource(ILogger<Engine> logger, string source);

    [LoggerMessage(LogLevel.Information, "Registered {sourceCount} command source(s): {sources}")]
    static partial void LogRegisteredSourceCountCommandSourceSSources(
        ILogger<Engine> logger,
        int sourceCount,
        IEnumerable<string> sources);

    [LoggerMessage(LogLevel.Information, "Starting command source {source}")]
    static partial void LogStartingCommandSourceSource(ILogger<Engine> logger, string source);
}