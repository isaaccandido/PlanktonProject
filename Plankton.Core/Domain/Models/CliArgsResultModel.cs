namespace Plankton.Core.Domain.Models;

public sealed class CliArgsResultModel
{
    public Dictionary<string, object?> Values { get; } = new();
    public bool HasHelp { get; init; }
}