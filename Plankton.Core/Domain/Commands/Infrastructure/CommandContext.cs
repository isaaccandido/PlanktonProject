using Plankton.Core.Domain.Models;

namespace Plankton.Core.Domain.Commands.Infrastructure;

public sealed class CommandContext
{
    public required CommandModel Command { get; init; }
    public string? Token { get; init; }
    public required string CorrelationId { get; init; } = string.Empty;
}