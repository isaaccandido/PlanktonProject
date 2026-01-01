namespace Plankton.Core.Domain.CLI.Models;

public sealed class CliSchema
{
    public Dictionary<string, CliOption>? Options { get; init; } = new();
}