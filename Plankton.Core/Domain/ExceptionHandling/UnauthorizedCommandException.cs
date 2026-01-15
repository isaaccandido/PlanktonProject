namespace Plankton.Core.Domain.ExceptionHandling;

public sealed class UnauthorizedCommandException() : DomainException("Unauthorized");