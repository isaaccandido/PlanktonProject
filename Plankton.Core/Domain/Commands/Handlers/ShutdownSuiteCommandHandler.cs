using Plankton.Core.Domain.Models;
using Plankton.Core.Interfaces;

namespace Plankton.Core.Domain.Commands.Handlers;

public sealed class ShutdownSuiteCommandHandler(Func<Engine> engineFactory) : ICommandHandler
{
    private readonly Func<Engine> _engineFactory =
        engineFactory ?? throw new ArgumentNullException(nameof(engineFactory));

    public string CommandName => "shutdown-suite";
    public string Description => "Stops the suite altogether. This is like a red mushroom emergency button.";
    public int MinArgs => 0;
    public string[]? FixedArgs => [];

    public async Task<object?> HandleAsync(CommandModel command)
    {
        var engine = _engineFactory();
        await engine.ShutdownAsync();

        return null;
    }
}