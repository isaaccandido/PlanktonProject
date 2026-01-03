using Plankton.Core.Domain.Models;
using Plankton.Core.Interfaces;

namespace Plankton.Core.Domain.Commands.Handlers;

public sealed class ShutdownCommandHandler(Func<Engine> engineFactory) : ICommandHandler
{
    private readonly Func<Engine> _engineFactory =
        engineFactory ?? throw new ArgumentNullException(nameof(engineFactory));

    public string CommandName => "shutdown";

    public async Task<object?> HandleAsync(CommandModel command)
    {
        var engine = _engineFactory();
        await engine.ShutdownAsync();

        return null;
    }
}