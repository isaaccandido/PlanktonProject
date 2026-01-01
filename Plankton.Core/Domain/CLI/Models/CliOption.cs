namespace Plankton.Core.Domain.CLI.Models;

public sealed class CliOption
{
    public string Type { get; init; } = null!;
    public int? MinArgs { get; init; }
    public int? MaxArgs { get; init; }
    public bool Required { get; init; }
    public object? Default { get; init; }
    public string Help { get; init; } = null!;
    public string[]? Values { get; init; }
}