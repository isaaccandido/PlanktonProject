using Microsoft.Extensions.DependencyInjection;
using Plankton.Core.Domain.Models;
using Plankton.Core.Interfaces;

namespace Plankton.Core.Domain.Commands.Handlers;

public sealed class ListCommandsCommandHandler(IServiceProvider provider) : ICommandHandler
{
    public string CommandName => "list-commands";
    public int MinArgs => 0;
    public string[]? FixedArgs => [];
    public string? Description => "Lists all available commands.";

    public Task<object?> HandleAsync(CommandModel command)
    {
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
}