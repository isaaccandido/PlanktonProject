namespace Plankton.Core.Domain.Models;

public class CommandRequestModel
{
    public required string Name { get; init; }
    public string[] Args { get; init; } = [];
}