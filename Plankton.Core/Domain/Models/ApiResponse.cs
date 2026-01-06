namespace Plankton.Core.Domain.Models;

public sealed record ApiResponse
{
    public required object Data { get; init; }
}