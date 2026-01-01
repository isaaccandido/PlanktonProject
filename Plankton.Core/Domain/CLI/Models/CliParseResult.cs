namespace Plankton.Core.Domain.CLI.Models;

public sealed class CliParseResult
{
    public Dictionary<string, object?> Values { get; } = new();
    public bool HasHelp { get; init; }
}