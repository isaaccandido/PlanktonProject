namespace Plankton.Core.Domain.Models;

public sealed class CliSchemaModel
{
    public Dictionary<string, CliOptionModel>? Options { get; init; } = new();
}