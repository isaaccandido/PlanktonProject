using Plankton.Core.Enums;

namespace Plankton.Core.Domain.Models;

public sealed record CommandModel
{
    public required string Name { get; init; }
    public IReadOnlyList<string> Args { get; init; } = [];
    public SourceType? Source { get; init; }
    public string? SenderId { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}