using Microsoft.Extensions.DependencyInjection;
using Plankton.Core.Domain.Models;
using Plankton.Core.Interfaces;

namespace Plankton.Core.Domain.Commands.Handlers;

public sealed class ListCommandsCommandHandler(IServiceProvider provider) : ICommandHandler
{
    public string CommandName => "list-commands";
    public int MinArgs => 0;
    public string[]? FixedArgs => [];

    public Task<object?> HandleAsync(CommandModel command)
    {
        var commandList = provider.GetServices<ICommandHandler>()
            .Where(h => h.GetType() != typeof(ListCommandsCommandHandler))
            .Select(h => h.CommandName)
            .ToList();

        return Task.FromResult<object?>(new Dictionary<string, object?>
        {
            { "available-commands", commandList }
        });
    }
}