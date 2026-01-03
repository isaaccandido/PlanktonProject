namespace Plankton.Core.Domain.Models;

public sealed record ApiResponse<T>
{
    public required T Data { get; init; }
}