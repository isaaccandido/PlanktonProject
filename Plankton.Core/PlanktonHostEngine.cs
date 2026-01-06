using System.Text.Json;
using Microsoft.Extensions.Logging;
using Plankton.Bots;
using Plankton.Core.Domain.Commands.Infrastructure;
using Plankton.Core.Domain.Models;
using Plankton.Core.Enums;
using Plankton.Core.Interfaces;

namespace Plankton.Core;

public partial class PlanktonHostEngine(
    ILogger<PlanktonHostEngine> logger,
    CommandBus commandBus,
    BotEngine botEngine)
{
    public required CliArgsResultModel CliArgs { get; set; } // TODO find an use for CLI args

    private readonly List<ICommandSource> _commandSources = [];
    private readonly CancellationTokenSource _cts = new();
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

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
        LogEngineStarted(logger);

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

        _ = botEngine.RunAsync(_cts.Token);

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

        var receivedArgs = string.Join(", ", context.Command.Args);

        LogReceivedCommandCommand(
            logger,
            context.Command.Name,
            receivedArgs,
            context.Command.Source
        );

        var response = await commandBus.DispatchAsync(context);

        var serializedResponse = JsonSerializer.Serialize(response, _jsonOptions);

        LogResponseResponse(logger, serializedResponse);

        return response;
    }


    public async Task ShutdownAsync()
    {
        LogShutdownRequested(logger);

        foreach (var source in _commandSources) source.CommandReceived -= HandleCommandAsync;

        await _cts.CancelAsync();
        await Task.CompletedTask;
    }

    [LoggerMessage(LogLevel.Information, "Engine started")]
    static partial void LogEngineStarted(ILogger<PlanktonHostEngine> logger);

    [LoggerMessage(LogLevel.Information, "Engine stopped")]
    static partial void LogEngineStopped(ILogger<PlanktonHostEngine> logger);

    [LoggerMessage(LogLevel.Information, "Received command: [{command}] with args [{receivedArgs}] via {source}")]
    static partial void LogReceivedCommandCommand(
        ILogger<PlanktonHostEngine> logger,
        string command,
        string receivedArgs,
        SourceType? source);

    [LoggerMessage(LogLevel.Information, "Registering command source {source}")]
    static partial void LogRegisteringCommandSourceSource(ILogger<PlanktonHostEngine> logger, string source);

    [LoggerMessage(LogLevel.Information, "Registered {sourceCount} command source(s): {sources}")]
    static partial void LogRegisteredSourceCountCommandSourceSSources(
        ILogger<PlanktonHostEngine> logger,
        int sourceCount,
        IEnumerable<string> sources);

    [LoggerMessage(LogLevel.Information, "Starting command source '{source}'")]
    static partial void LogStartingCommandSourceSource(ILogger<PlanktonHostEngine> logger, string source);

    [LoggerMessage(LogLevel.Information, "Response: {response}")]
    static partial void LogResponseResponse(ILogger<PlanktonHostEngine> logger, string response);

    [LoggerMessage(LogLevel.Information, "Shutdown requested")]
    static partial void LogShutdownRequested(ILogger<PlanktonHostEngine> logger);
}