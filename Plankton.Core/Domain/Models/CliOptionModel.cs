namespace Plankton.Core.Domain.Models;

public sealed class CliOptionModel
{
    public string Type { get; init; } = null!;
    public int? MinArgs { get; init; }
    public int? MaxArgs { get; init; }
    public bool Required { get; init; }
    public object? Default { get; init; }
    public string Help { get; init; } = null!;
    public string[]? Values { get; init; }
}