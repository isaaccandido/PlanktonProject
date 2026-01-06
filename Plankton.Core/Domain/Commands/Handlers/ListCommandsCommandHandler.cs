using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Plankton.Core.Domain.Models;
using Plankton.Core.Enums;
using Plankton.Core.Interfaces;

namespace Plankton.Core.Domain.Commands.Handlers;

public sealed partial class ListCommandsCommandHandler(
    IServiceProvider provider,
    ILogger<ListCommandsCommandHandler> logger
) : ICommandHandler
{
    public string CommandName => "list-commands";

    public string? Description => """
                                  Lists all available commands in the suite along with their metadata.

                                  Each command includes:
                                  - minimumArgsCount : Minimum number of arguments required.
                                  - description      : A brief description of what the command does.
                                  - possibleArguments: List of fixed arguments the command accepts (if any).

                                  Example usage:
                                  list-commands
                                  """;

    public int MinArgs => 0;
    public string[] FixedArgs => [];

    public Task<object?> HandleAsync(CommandModel command)
    {
        LogListingAllCommandsSourceSource(logger, command.Source);

        var commandList = provider.GetServices<ICommandHandler>()
            .ToDictionary(
                handler => handler.CommandName,
                handler =>
                {
                    var dict = new Dictionary<string, object?> { ["minimumArgsCount"] = handler.MinArgs };
                    if (!string.IsNullOrWhiteSpace(handler.Description)) dict["description"] = handler.Description;
                    if (handler.FixedArgs is { Length: > 0 }) dict["possibleArguments"] = handler.FixedArgs;

                    return dict;
                }
            );

        var response = new Dictionary<string, object?>
        {
            ["availableCommands"] = commandList
        };

        return Task.FromResult<object?>(response);
    }

    [LoggerMessage(LogLevel.Information, "Listing all commands (Source: {source})")]
    static partial void LogListingAllCommandsSourceSource(
        ILogger<ListCommandsCommandHandler> logger,
        SourceType? source
    );
}